using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Services;
using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Web.Tests;

public partial class WebReviewPipelineTests
{
    [Fact]
    public async Task PinnedDisplaySurvivesCleanup_WithoutSendingSourceToReviewer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "turtle-pipeline-display-" + Guid.NewGuid().ToString("N"));
        Repository.Init(directory);
        var filePath = Path.Combine(directory, "Service.cs");
        File.WriteAllText(Path.Combine(directory, "Fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        string baseSha, headSha;
        var source = "// SOURCE_ONLY_CANARY\nclass Service\n{\n    public int Read() => 2;\n}\n";
        using (var repo = new Repository(directory))
        {
            var author = new Signature("Fixture", "fixture@example.invalid", DateTimeOffset.UnixEpoch);
            File.WriteAllText(filePath, source.Replace("=> 2", "=> 1"));
            Commands.Stage(repo, "*"); baseSha = repo.Commit("base", author, author).Sha;
            File.WriteAllText(filePath, source);
            Commands.Stage(repo, "*"); headSha = repo.Commit("head", author, author).Sha;
        }
        var store = new ReviewJobStore();
        var id = store.CreateJob("https://github.com/fixture/repo/pull/1");
        var compilation = CSharpCompilation.Create("Fixture", [CSharpSyntaxTree.ParseText(source, path: filePath)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var runner = new CaptureRunner();
        var rubric = Path.GetTempFileName();
        File.WriteAllText(rubric, "Review facts only");
        try
        {
            var pipeline = new WebReviewPipeline(new StubPrFetcher(new(true, true, baseSha, headSha, "feature", ["Service.cs"], "")),
                new DeleteOnCleanup(directory), new StubCompilationLoader(compilation), runner, new Arbiter(),
                new TurtleShellGuard(new CouncilOptions()), new RubricLoader(), store, new CouncilOptions(), rubric);
            await pipeline.ExecuteAsync(id);
            Assert.False(Directory.Exists(directory));
            Assert.True(store.TryGetResult(id, out var result));
            Assert.NotNull(result);
            Assert.DoesNotContain("SOURCE_ONLY_CANARY", System.Text.Json.JsonSerializer.Serialize(runner.Payload));
            store.TryGetJob(id, out var job);
            Assert.Contains("SOURCE_ONLY_CANARY", Assert.Single(job!.Changes!.Files).Source);
            Assert.Equal(headSha, job.Changes.Snapshot.HeadSha);
            Assert.Equal(4, Assert.Single(job.Changes.Anchors).StartLine);
        }
        finally { new RepoCloner().Cleanup(directory); File.Delete(rubric); }
    }

    private sealed class DeleteOnCleanup(string directory) : IRepoCloner
    {
        public Task<string> CloneAsync(string url, string branch, string? targetDir = null, CancellationToken ct = default) => Task.FromResult(directory);
        public void Cleanup(string path) => new RepoCloner().Cleanup(path);
        public void Dispose() { }
    }
    private sealed class CaptureRunner : IStreamingPersonaRunner
    {
        public RoslynPayload? Payload;
        public async Task<IReadOnlyList<PersonaVerdict>> RunWithCallbacksAsync(RoslynPayload payload, string rubric,
            Func<PersonaRole, Task> onStarted, Func<PersonaVerdict, Task> onCompleted,
            Func<PersonaRole, string, Task> onFailed, CancellationToken ct = default)
        {
            Payload = payload;
            var verdict = new PersonaVerdict(PersonaRole.SecurityAuditor,
                [new(PersonaRole.SecurityAuditor, Severity.Warning, "Inspect member", "Advisory", "Wrong.cs:999", ["Service.Read"])]);
            await onStarted(verdict.Persona);
            await onCompleted(verdict);
            return [verdict];
        }
    }
}
