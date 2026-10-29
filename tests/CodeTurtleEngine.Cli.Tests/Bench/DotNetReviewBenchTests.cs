using CodeTurtleEngine.Cli.Bench;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class DotNetReviewBenchTests
{
    [Fact]
    public void Renders_Track_Report_Separating_All_Three_Tracks()
    {
        var trackA = new TrackScore(
            BenchmarkTrack.TrackA_HumanReviews,
            TotalPullRequests: 20,
            new RunScore(Findings: 50, Citations: 40, Unverified: 0, Invented: 0, FindingsWithInvented: 0, Matched: 25, ExpectedCount: 50));

        var trackB = new TrackScore(
            BenchmarkTrack.TrackB_BugFixBlame,
            TotalPullRequests: 15,
            new RunScore(Findings: 30, Citations: 25, Unverified: 0, Invented: 0, FindingsWithInvented: 0, Matched: 12, ExpectedCount: 30));

        var trackC = new TrackScore(
            BenchmarkTrack.TrackC_InjectedDefects,
            TotalPullRequests: 10,
            new RunScore(Findings: 20, Citations: 20, Unverified: 0, Invented: 0, FindingsWithInvented: 0, Matched: 18, ExpectedCount: 20));

        var report = new DotNetReviewBenchReport("2026-10-29T12:00:00Z", new[] { trackA, trackB, trackC });
        var md = report.RenderMarkdown();

        Assert.Contains("DotNetReviewBench Evaluation Report", md);
        Assert.Contains("TrackA_HumanReviews", md);
        Assert.Contains("TrackB_BugFixBlame", md);
        Assert.Contains("TrackC_InjectedDefects", md);
        Assert.Contains("50.0%", md); // Track A precision: 25/50
        Assert.Contains("90.0%", md); // Track C recall: 18/20
    }
}
