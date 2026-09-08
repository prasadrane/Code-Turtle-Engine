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
}
