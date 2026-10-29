using System.Text;

namespace CodeTurtleEngine.Cli.Bench;

public enum BenchmarkTrack
{
    TrackA_HumanReviews,
    TrackB_BugFixBlame,
    TrackC_InjectedDefects
}

public sealed record TrackScore(
    BenchmarkTrack Track,
    int TotalPullRequests,
    RunScore AggregateScore);

public sealed record DotNetReviewBenchReport(
    string TimestampUtc,
    IReadOnlyList<TrackScore> TrackScores)
{
    public string RenderMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# DotNetReviewBench Evaluation Report");
        sb.AppendLine();
        sb.AppendLine($"Timestamp: {TimestampUtc}");
        sb.AppendLine();
        sb.AppendLine("| Track | PRs | Findings | Matched | Precision | Recall | Invented Citations |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var ts in TrackScores)
        {
            var p = ts.AggregateScore.Precision.HasValue ? $"{ts.AggregateScore.Precision.Value:P1}" : "N/A";
            var r = ts.AggregateScore.Recall.HasValue ? $"{ts.AggregateScore.Recall.Value:P1}" : "N/A";
            var inv = ts.AggregateScore.InventedRate.HasValue ? $"{ts.AggregateScore.InventedRate.Value:P1}" : "0.0%";
            sb.AppendLine($"| {ts.Track} | {ts.TotalPullRequests} | {ts.AggregateScore.Findings} | {ts.AggregateScore.Matched ?? 0} | {p} | {r} | {inv} |");
        }
        return sb.ToString();
    }
}
