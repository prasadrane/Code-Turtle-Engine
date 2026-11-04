using System.Text;
using System.Globalization;

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
            var p = Percentage(ts.AggregateScore.Precision, "N/A");
            var r = Percentage(ts.AggregateScore.Recall, "N/A");
            var inv = Percentage(ts.AggregateScore.InventedRate, "0.0%");
            sb.AppendLine($"| {ts.Track} | {ts.TotalPullRequests} | {ts.AggregateScore.Findings} | {ts.AggregateScore.Matched ?? 0} | {p} | {r} | {inv} |");
        }
        return sb.ToString();
    }

    private static string Percentage(double? value, string fallback) => value.HasValue
        ? (value.Value * 100).ToString("F1", CultureInfo.InvariantCulture) + "%"
        : fallback;
}
