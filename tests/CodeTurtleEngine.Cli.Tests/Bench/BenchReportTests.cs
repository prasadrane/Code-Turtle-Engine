using CodeTurtleEngine.Cli.Bench;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class BenchReportTests
{
    private static readonly BenchResults Results = new(
        new BenchMetadata(DateTimeOffset.UnixEpoch, 1, new[] { "token-plan:qwen3.8-flash" }, new Dictionary<string, string> { ["e1"] = "abc" }),
        new[]
        {
            new BenchRow("e1", BenchArm.Naive, 1, new RunScore(4, 10, 6, 3, 2, 1, 2), 1000, 3, null),
            new BenchRow("e1", BenchArm.Grounded, 1, new RunScore(4, 8, 0, 0, 0, 2, 2), 2000, 2, null),
            new BenchRow("e1", BenchArm.CodeTurtle, 1, new RunScore(4, 8, 0, 0, 0, 2, 2), 2000, 2, null),
            new BenchRow("e2", BenchArm.Naive, 1, null, 50, 0, "quorum")
        },
        new[] { new SkippedEntry("e3", "compile failed") });

    [Fact]
    public void Markdown_Summarizes_Each_Arm_Errors_Skips_And_Limits()
    {
        var md = BenchReport.RenderMarkdown(Results);

        Assert.Contains("| Naive | 2 | 1 | 0 |", md);       // runs, errors, degraded
        Assert.Contains("30.0%", md);                       // naive invented rate 3/10
        Assert.Contains("| Grounded | 1 | 0 | 1 |", md);    // 2/3 personas -> degraded
        Assert.Contains("e3", md);
        Assert.Contains("compile failed", md);
        Assert.Contains("## Limits", md);
        Assert.Contains("token-plan:qwen3.8-flash", md);
    }

    [Fact]
    public void Write_Produces_Json_And_Markdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-report-" + Guid.NewGuid().ToString("N"));
        var (json, md) = BenchReport.Write(Results, dir);
        Assert.Contains("\"invented\": 3", File.ReadAllText(json));
        Assert.True(File.Exists(md));
        Directory.Delete(dir, true);
    }
}
