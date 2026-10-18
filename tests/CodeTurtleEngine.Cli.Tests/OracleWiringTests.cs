using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace CodeTurtleEngine.Cli.Tests;

public class OracleWiringTests
{
    private sealed class Personas : IPersonaRunner
    {
        public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources, string rubric, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PersonaVerdict>>([new(PersonaRole.SecurityAuditor,
            [new(PersonaRole.SecurityAuditor, Severity.Warning, "mixed", "d", "A.cs:1", ["Demo.Outside.Real", "Demo.Outside.Fake"]),
             new(PersonaRole.SecurityAuditor, Severity.Info, "uncited", "d", "A.cs:2", [])])]);
    }
    [Fact]
    public void Labels_In_Secondary_Project_Are_Enriched_Without_Flattening()
    {
        var a = CSharpCompilation.Create("A");
        var b = CSharpCompilation.Create("B", [CSharpSyntaxTree.ParseText("class B {\n void MethodInB() {}\n}", path: "B.cs")]);
        var result = CodeTurtleEngine.Cli.Bench.ExpectedDefectEnricher.Enrich(new[] { a, b },
            [new CodeTurtleEngine.Cli.Bench.ExpectedDefect("B.cs", 2, "SomeDefect")]);
        Assert.Equal("MethodInB", Assert.Single(result!).Method);
    }
    [Theory]
    [InlineData(GuardMode.Strip, false, 2, 1)]
    [InlineData(GuardMode.Strip, true, 1, 1)]
    [InlineData(GuardMode.Flag, true, 2, 2)]
    public async Task Compilation_Outside_Prompt_Is_Audited(GuardMode mode, bool strict, int count, int citations)
    {
        var directory = Path.Combine(Path.GetTempPath(), "oracle-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var rubric = Path.Combine(directory, "rubric.md");
            await File.WriteAllTextAsync(rubric, "rubric");
            var compilation = CSharpCompilation.Create("Demo", [CSharpSyntaxTree.ParseText(
                "namespace Demo; public class Outside { public void Real() {} }", path: "Outside.cs")],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            var options = new TurtleOptions { RubricPath = rubric, Council = new CouncilOptions {
                Guard = mode, RequireCitationsInStripMode = strict, MaxFiles = 0 } };
            var result = await new ReviewPipeline(new FakeDiff([]), new FakeLoader(compilation), new Personas(),
                new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(directory), options)
                .RunAsync(directory, "Demo.csproj", null);
            Assert.Empty(result.Payload.Files);
            Assert.Equal(count, result.Verdict.Synthesized.Count);
            Assert.Equal(citations, result.Verdict.Synthesized.Single(f => f.Title == "mixed").CitedSymbolFqns.Count);
            Assert.Contains(result.Verdict.GuardAudit, a => a.Evidence == "Compilation" && a.CitedFqn == "Demo.Outside.Real");
            Assert.Contains(result.Verdict.GuardAudit, a => a.Evidence == "Rejected" && a.CitedFqn == "Demo.Outside.Fake");
            Assert.Equal(2, result.Verdict.Personas.Single().Findings[0].CitedSymbolFqns.Count);
            Assert.True(File.Exists(Path.Combine(result.ArtifactDir, "finding-audit.json")));
        }
        finally { Directory.Delete(directory, true); }
    }
}
