using System.Globalization;
using System.Text;

namespace CodeTurtleEngine.Cli.Bench;

/// <summary>Repeat-level variation, keeping corpus heterogeneity separate from sampling variation.</summary>
public static class BenchVariance
{
    public static void Append(StringBuilder sb, BenchResults results)
    {
        sb.AppendLine();
        sb.AppendLine("## Variation across repetitions");
        sb.AppendLine();
        sb.AppendLine("Only repetitions complete across all three arms and covering every prepared entry with no errors, degraded councils, or skipped entries are included. Missing samples are never imputed. Pooled metrics aggregate across all entries in a repetition; mean metrics average only entries where the metric is defined. Sample variance uses n−1 and rates on the 0–1 scale; SD is in percentage points. Fewer than two defined samples gives n/a variance. Replayed answers must be independent model responses to support sampling claims.");
        sb.AppendLine();
        sb.AppendLine("| Arm | Metric | Complete repetitions | Defined samples | Mean | Sample variance | Sample SD | Min–max |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        var allArms = Enum.GetValues<BenchArm>();
        var completeReps = results.Skipped.Count > 0 || results.Metadata.EntryHeads.Count == 0
            ? Array.Empty<int>()
            : results.Rows.Select(r => r.Repetition).Distinct().OrderBy(x => x)
                .Where(rep => allArms.All(arm =>
                {
                    var armRows = results.Rows.Where(r => r.Arm == arm && r.Repetition == rep).ToList();
                    return armRows.Count == results.Metadata.EntryHeads.Count
                        && armRows.Select(r => r.EntryId).ToHashSet(StringComparer.Ordinal).SetEquals(results.Metadata.EntryHeads.Keys)
                        && armRows.All(r => r.Error is null && !r.Degraded && r.Score is not null);
                })).ToArray();

        foreach (var arm in allArms)
        {
            if (!results.Rows.Any(r => r.Arm == arm)) continue;
            var complete = completeReps
                .Select(rep => results.Rows.Where(r => r.Arm == arm && r.Repetition == rep).Select(r => r.Score!).ToArray())
                .ToArray();
            AddMetric(sb, arm, "Pooled invented rate", complete.Length, complete.Select(scores =>
            {
                var citations = scores.Sum(s => s.Citations);
                return citations == 0 ? (double?)null : (double)scores.Sum(s => s.Invented) / citations;
            }));
            AddMetric(sb, arm, "Pooled precision", complete.Length, complete.Select(scores =>
            {
                var labeled = scores.Where(s => s.Matched is not null).ToArray();
                var findings = labeled.Sum(s => s.Findings);
                return findings == 0 ? (double?)null : (double)labeled.Sum(s => s.Matched!.Value) / findings;
            }));
            AddMetric(sb, arm, "Mean precision", complete.Length, complete.Select(scores => Average(scores.Select(s => s.Precision))));
            AddMetric(sb, arm, "Pooled recall", complete.Length, complete.Select(scores =>
            {
                var withExpected = scores.Where(s => s.Matched is not null && s.ExpectedCount is > 0).ToArray();
                var expected = withExpected.Sum(s => s.ExpectedCount!.Value);
                return expected == 0 ? (double?)null : (double)withExpected.Sum(s => s.Matched!.Value) / expected;
            }));
            AddMetric(sb, arm, "Mean recall", complete.Length, complete.Select(scores => Average(scores.Select(s => s.Recall))));
        }
    }

    private static double? Average(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return present.Length == 0 ? null : present.Average();
    }

    private static void AddMetric(StringBuilder sb, BenchArm arm, string metric, int completeCount, IEnumerable<double?> values)
    {
        var samples = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var mean = samples.Length == 0 ? (double?)null : samples.Average();
        var variance = samples.Length < 2 ? (double?)null
            : samples.Sum(v => Math.Pow(v - mean!.Value, 2)) / (samples.Length - 1);
        string Pct(double? v) => v?.ToString("0.0%", CultureInfo.InvariantCulture) ?? "n/a";
        var sd = variance is null ? "n/a" : (100 * Math.Sqrt(variance.Value)).ToString("0.0", CultureInfo.InvariantCulture) + " pp";
        var range = samples.Length == 0 ? "n/a" : $"{Pct(samples.Min())}–{Pct(samples.Max())}";
        sb.AppendLine($"| {arm} | {metric} | {completeCount} | {samples.Length} | {Pct(mean)} | {variance?.ToString("0.######", CultureInfo.InvariantCulture) ?? "n/a"} | {sd} | {range} |");
    }
}
