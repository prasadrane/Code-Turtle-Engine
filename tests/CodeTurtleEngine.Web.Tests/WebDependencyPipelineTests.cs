using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Services;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Web.Tests;

public partial class WebReviewPipelineTests
{
    private sealed class RecordingPreparer(List<string> calls, bool fail) : IWebDependencyPreparer
    {
        public Task PrepareAsync(string projectPath, CancellationToken ct)
        {
            calls.Add("restore");
            return fail ? Task.FromException(new InvalidOperationException("private raw restore diagnostic")) : Task.CompletedTask;
        }
    }

    private sealed class RecordingLoader(List<string> calls) : ICompilationLoader
    {
        public Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
        {
            calls.Add("compile");
            return Task.FromResult(new LoadedCompilation(CSharpCompilation.Create("Test"), new EmptyWorkspace()));
        }
        private sealed class EmptyWorkspace : IDisposable { public void Dispose() { } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DependenciesArePreparedBeforeCompilationAndFailureStopsReview(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-web-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Test.csproj"), "<Project />");
        var rubric = Path.Combine(root, "rubric.md");
        await File.WriteAllTextAsync(rubric, "rubric");
        try
        {
            var calls = new List<string>();
            var store = new ReviewJobStore();
            var job = store.CreateJob("https://github.com/owner/repo/pull/1");
            var cloner = new StubRepoCloner(root);
            var pipeline = new WebReviewPipeline(new StubPrFetcher(new(true, true, "base", "head", "branch", [], "")),
                cloner, new RecordingLoader(calls), new StubStreamingRunner(), new Arbiter(),
                new TurtleShellGuard(new()), new RubricLoader(), store, new(), rubric,
                new RecordingPreparer(calls, fail));
            await pipeline.ExecuteAsync(job);
            Assert.Equal(fail ? new[] { "restore" } : new[] { "restore", "compile" }, calls);
            Assert.True(cloner.CleanedUp);
            if (fail)
            {
                Assert.True(store.TryGetError(job, out var error));
                Assert.DoesNotContain("private raw", error);
                Assert.False(store.TryGetResult(job, out _));
            }
            else Assert.True(store.TryGetResult(job, out _));
        }
        finally { Directory.Delete(root, true); }
    }
}
