using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli.Tests;

public class ReviewPipelineTests
{
    [Fact]
    public async Task Orchestrates_And_Writes_Artifacts()
    {
        var home = Path.Combine(Path.GetTempPath(), "turtle-pipe-" + Guid.NewGuid().ToString("N"));
        var rubricPath = Path.GetTempFileName();
        File.WriteAllText(rubricPath, "# Review Rubric v1");
        var options = new TurtleOptions { RubricPath = rubricPath, Council = new CouncilOptions { Quorum = 2, Guard = GuardMode.Strip } };

        var pipeline = new ReviewPipeline(
            new FakeDiff(new[] { "PaymentService.cs" }),
            new FakeLoader(),
            new FakePersonas(),
            new Arbiter(),
            new TurtleShellGuard(options.Council),
            new RubricLoader(),
            new ArtifactWriter(home),
            options);

        var result = await pipeline.RunAsync("/repo", "/repo/SampleRepo.csproj", null);

        Assert.Contains("# Code Turtle Review", result.Markdown);
        Assert.True(File.Exists(Path.Combine(result.ArtifactDir, "review.md")));

        // FakePersonas cites "SampleRepo.DataAccess", which is absent from the stub
        // compilation's allow-list, so the guard strips it: the rendered markdown must
        // NOT contain the finding and must report no issues. Proves the pipeline renders
        // guarded.KeptFindings, not the pre-guard merged list.
        Assert.DoesNotContain("SQL injection", result.Markdown);
        Assert.Contains("_No issues found._", result.Markdown);

        Directory.Delete(home, true);
    }

    [Theory]
    [InlineData(PromptMode.Naive, 1)]
    [InlineData(PromptMode.Grounded, 0)]
    public async Task Supplies_Changed_Source_Only_In_Naive_Mode(PromptMode mode, int expectedSources)
    {
        var home = Path.Combine(Path.GetTempPath(), "turtle-pipe-" + Guid.NewGuid().ToString("N"));
        var rubricPath = Path.GetTempFileName();
        File.WriteAllText(rubricPath, "# Review Rubric v1");
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            "namespace Demo { public class A { public void M() { } } }", path: "/repo/src/A.cs");
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Demo", new[] { tree });
        var options = new TurtleOptions { RubricPath = rubricPath, Council = new CouncilOptions { Quorum = 1, PromptMode = mode } };
        var personas = new RecordingPersonas();

        var result = await new ReviewPipeline(new FakeDiff(new[] { "src/A.cs" }), new FakeLoader(compilation), personas,
            new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(home), options)
            .RunAsync("/repo", "/repo/Demo.csproj", null);

        Assert.Equal(expectedSources, personas.LastSources!.Count);
        if (expectedSources == 1)
        {
            Assert.Equal("src/A.cs", personas.LastSources[0].Path);
            Assert.Contains("public class A", personas.LastSources[0].Text);
        }
        Assert.Equal("Demo", result.Payload.RepoSlug);
        Directory.Delete(home, true);
    }
}
