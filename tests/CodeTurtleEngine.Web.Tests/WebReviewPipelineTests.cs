using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Web.Tests;

public class WebReviewPipelineTests
{
    private sealed class StubPrFetcher : IGitHubPrFetcher
    {
        public GitHubPrDetails Details { get; set; }

        public StubPrFetcher(GitHubPrDetails details)
        {
            Details = details;
        }

        public Task<GitHubPrDetails> FetchAsync(string owner, string repo, int pullNumber, CancellationToken ct = default)
            => Task.FromResult(Details);
    }

    private sealed class StubRepoCloner : IRepoCloner
    {
        public bool Cloned { get; private set; }
        public bool CleanedUp { get; private set; }
        private readonly string _dir;

        public StubRepoCloner(string dir)
        {
            _dir = dir;
        }

        public Task<string> CloneAsync(string cloneUrl, string headBranch, string? targetDir = null, CancellationToken ct = default)
        {
            Cloned = true;
            return Task.FromResult(_dir);
        }

        public void Cleanup(string directory)
        {
            CleanedUp = true;
        }

        public void Dispose() { }
    }

    private sealed class StubCompilationLoader : ICompilationLoader
    {
        public Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedCompilation(CSharpCompilation.Create("Stub"), new NoopDisposable()));

        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }

    private sealed class StubStreamingRunner : IStreamingPersonaRunner
    {
        public async Task<IReadOnlyList<PersonaVerdict>> RunWithCallbacksAsync(
            RoslynPayload payload,
            string rubric,
            Func<PersonaRole, Task> onStarted,
            Func<PersonaVerdict, Task> onCompleted,
            Func<PersonaRole, string, Task> onFailed,
            CancellationToken ct = default)
        {
            await onStarted(PersonaRole.AllocationsPerformance);
            var finding = new ReviewFinding(
                PersonaRole.AllocationsPerformance,
                Severity.Warning,
                "Boxing allocation",
                "Boxing in loop",
                "Loop.cs:10",
                Array.Empty<string>());
            var verdict = new PersonaVerdict(PersonaRole.AllocationsPerformance, new[] { finding });
            await onCompleted(verdict);
            return new[] { verdict };
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrNotAccessible_EmitsValidating_ThenErrorEvent_WithoutCloning()
    {
        var fetcher = new StubPrFetcher(new GitHubPrDetails(
            IsAccessible: false,
            IsDotNet: false,
            BaseSha: "",
            HeadSha: "",
            HeadBranch: "",
            ChangedFiles: Array.Empty<string>(),
            LanguageMessage: "Inaccessible"));

        var cloner = new StubRepoCloner("/tmp/dummy");
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/owner/repo/pull/1");

        var tempRubric = Path.GetTempFileName();
        File.WriteAllText(tempRubric, "rubric");

        try
        {
            var pipeline = new WebReviewPipeline(
                fetcher,
                cloner,
                new StubCompilationLoader(),
                new StubStreamingRunner(),
                new Arbiter(),
                new TurtleShellGuard(new CouncilOptions()),
                new RubricLoader(),
                store,
                new CouncilOptions(),
                tempRubric);

            await pipeline.ExecuteAsync(jobId);

            Assert.False(cloner.Cloned);
            Assert.True(store.TryGetError(jobId, out var error));
            Assert.NotNull(error);

            var reader = store.GetReader(jobId);
            var events = new List<SseEvent>();
            await foreach (var evt in reader.ReadAllAsync())
            {
                events.Add(evt);
            }

            Assert.Equal(2, events.Count);
            Assert.Equal(SseEventTypes.Phase, events[0].Type);
            Assert.Equal(SseEventTypes.Error, events[1].Type);
        }
        finally
        {
            if (File.Exists(tempRubric)) File.Delete(tempRubric);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrNotDotNet_EmitsValidating_ThenNotDotNetError_WithoutCloning()
    {
        var fetcher = new StubPrFetcher(new GitHubPrDetails(
            IsAccessible: true,
            IsDotNet: false,
            BaseSha: "base",
            HeadSha: "head",
            HeadBranch: "branch",
            ChangedFiles: new[] { "app.py" },
            LanguageMessage: "Python is dizzy"));

        var cloner = new StubRepoCloner("/tmp/dummy");
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/owner/repo/pull/2");

        var tempRubric = Path.GetTempFileName();
        File.WriteAllText(tempRubric, "rubric");

        try
        {
            var pipeline = new WebReviewPipeline(
                fetcher,
                cloner,
                new StubCompilationLoader(),
                new StubStreamingRunner(),
                new Arbiter(),
                new TurtleShellGuard(new CouncilOptions()),
                new RubricLoader(),
                store,
                new CouncilOptions(),
                tempRubric);

            await pipeline.ExecuteAsync(jobId);

            Assert.False(cloner.Cloned);
            Assert.True(store.TryGetError(jobId, out var error));
            Assert.Contains("Python is dizzy", error);

            var reader = store.GetReader(jobId);
            var events = new List<SseEvent>();
            await foreach (var evt in reader.ReadAllAsync())
            {
                events.Add(evt);
            }

            Assert.Equal(2, events.Count);
            Assert.Equal(SseEventTypes.Phase, events[0].Type);
            Assert.Equal(SseEventTypes.Error, events[1].Type);
        }
        finally
        {
            if (File.Exists(tempRubric)) File.Delete(tempRubric);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidDotNetPr_ExecutesAllPhasesAndStoresResult()
    {
        var tempRepoDir = Path.Combine(Path.GetTempPath(), "test_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRepoDir);
        File.WriteAllText(Path.Combine(tempRepoDir, "TestApp.csproj"), "<Project />");

        var fetcher = new StubPrFetcher(new GitHubPrDetails(
            IsAccessible: true,
            IsDotNet: true,
            BaseSha: "base",
            HeadSha: "head",
            HeadBranch: "branch",
            ChangedFiles: new[] { "Program.cs" },
            LanguageMessage: ""));

        var cloner = new StubRepoCloner(tempRepoDir);
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/owner/repo/pull/3");

        var tempRubric = Path.GetTempFileName();
        File.WriteAllText(tempRubric, "rubric content");

        try
        {
            var pipeline = new WebReviewPipeline(
                fetcher,
                cloner,
                new StubCompilationLoader(),
                new StubStreamingRunner(),
                new Arbiter(),
                new TurtleShellGuard(new CouncilOptions()),
                new RubricLoader(),
                store,
                new CouncilOptions(),
                tempRubric);

            await pipeline.ExecuteAsync(jobId);

            Assert.True(cloner.Cloned);
            Assert.True(cloner.CleanedUp);
            Assert.True(store.TryGetResult(jobId, out var result));
            Assert.NotNull(result);

            var reader = store.GetReader(jobId);
            var events = new List<SseEvent>();
            await foreach (var evt in reader.ReadAllAsync())
            {
                events.Add(evt);
            }

            // Validating, Cloning, Compiling, PersonaStarted, PersonaFinding, PersonaCompleted, ArbiterMerge, GuardAudit, Completed
            Assert.Contains(events, e => e.Type == SseEventTypes.Phase && ((PhaseEventData)e.Data).Phase == "validating");
            Assert.Contains(events, e => e.Type == SseEventTypes.Phase && ((PhaseEventData)e.Data).Phase == "cloning");
            Assert.Contains(events, e => e.Type == SseEventTypes.Phase && ((PhaseEventData)e.Data).Phase == "compiling");
            Assert.Contains(events, e => e.Type == SseEventTypes.PersonaStarted);
            Assert.Contains(events, e => e.Type == SseEventTypes.PersonaFinding);
            Assert.Contains(events, e => e.Type == SseEventTypes.PersonaCompleted);
            Assert.Contains(events, e => e.Type == SseEventTypes.ArbiterMerge);
            Assert.Contains(events, e => e.Type == SseEventTypes.GuardAudit);
            Assert.Contains(events, e => e.Type == SseEventTypes.Completed);
        }
        finally
        {
            if (File.Exists(tempRubric)) File.Delete(tempRubric);
            if (Directory.Exists(tempRepoDir)) Directory.Delete(tempRepoDir, true);
        }
    }
}
