using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Web.Tests;
public partial class WebReviewPipelineTests
{
    [Theory]
    [InlineData(GuardMode.Strip, false, false, 2, 1)]
    [InlineData(GuardMode.Strip, true, false, 1, 1)]
    [InlineData(GuardMode.Flag, true, false, 2, 2)]
    [InlineData(GuardMode.Strip, false, true, 0, 0)]
    public async Task Oracle_Policy_And_Compilation_Failure_Are_Explicit(GuardMode mode, bool strict, bool fail, int count, int citations)
    {
        var directory = Path.Combine(Path.GetTempPath(), "oracle-web-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Demo.csproj"), "<Project />");
            var rubric = Path.Combine(directory, "rubric.md");
            await File.WriteAllTextAsync(rubric, "rubric");
            var compilation = CSharpCompilation.Create("Demo", [CSharpSyntaxTree.ParseText(
                "namespace Demo; public class Outside { public void Real() {} }", path: "Outside.cs")],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            ReviewFinding Finding(string title, params string[] names) => new(PersonaRole.AllocationsPerformance,
                Severity.Warning, title, "d", "A.cs:1", names);
            var mixed = Finding("mixed", "Demo.Outside.Real", "Demo.Outside.Fake");
            var options = new CouncilOptions { Guard = mode, RequireCitationsInStripMode = strict, MaxFiles = 0 };
            var store = new ReviewJobStore();
            var id = store.CreateJob("https://github.com/o/r/pull/1");
            await new WebReviewPipeline(new StubPrFetcher(new GitHubPrDetails(true, true, "base", "head", "branch", [], "")),
                new StubRepoCloner(directory), new StubCompilationLoader(compilation, fail),
                new StubStreamingRunner([mixed, Finding("uncited")]), new Arbiter(), new TurtleShellGuard(options),
                new RubricLoader(), store, options, rubric).ExecuteAsync(id);
            var events = new List<SseEvent>();
            await foreach (var evt in store.GetReader(id).ReadAllAsync()) events.Add(evt);
            if (fail)
            {
                Assert.True(store.TryGetError(id, out var error));
                Assert.Contains("Compilation was incomplete", error);
                Assert.DoesNotContain(events, e => e.Type is SseEventTypes.Completed or SseEventTypes.PersonaStarted);
                return;
            }
            Assert.True(store.TryGetResult(id, out var result));
            Assert.Empty(result!.Payload.Files);
            Assert.Equal(count, result.Verdict.Synthesized.Count);
            Assert.Equal(citations, result.Verdict.Synthesized.Single(f => f.Title == "mixed").CitedSymbolFqns.Count);
            Assert.Contains(result.Verdict.GuardAudit, a => a.CitedFqn == "Demo.Outside.Real" && a.Evidence == "Compilation");
            Assert.Contains(result.Verdict.GuardAudit, a => a.CitedFqn == "Demo.Outside.Fake" && !a.Verified);
            Assert.Equal(2, result.Verdict.Personas.Single().Findings[0].CitedSymbolFqns.Count);
            var audit = (GuardAuditEventData)events.Single(e => e.Type == SseEventTypes.GuardAudit).Data;
            Assert.Equal(1, audit.Rejected);
            Assert.Equal(mode == GuardMode.Strip ? 1 : 0, audit.Stripped);
            Assert.All(events.Where(e => e.Type == SseEventTypes.PersonaFinding), e =>
                Assert.Equal("Provisional", ((PersonaFindingEventData)e.Data).VerificationStatus));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task Forked_Source_Cannot_Reach_The_Trusted_Host_Cloner()
    {
        var cloner = new StubRepoCloner("/unused");
        var store = new ReviewJobStore();
        var id = store.CreateJob("https://github.com/o/r/pull/1");
        await new WebReviewPipeline(new StubPrFetcher(new GitHubPrDetails(true,true,"base","head","branch",[],"",
            "https://github.com/untrusted/fork.git")), cloner, new StubCompilationLoader(), new StubStreamingRunner(),
            new Arbiter(), new TurtleShellGuard(new CouncilOptions()), new RubricLoader(), store, new CouncilOptions()).ExecuteAsync(id);
        Assert.False(cloner.Cloned);
        Assert.True(store.TryGetError(id, out var error));
        Assert.Equal("Review processing failed.", error);
    }
}
