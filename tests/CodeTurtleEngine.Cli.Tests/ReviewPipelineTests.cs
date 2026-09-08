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
        Directory.Delete(home, true);
    }
}
