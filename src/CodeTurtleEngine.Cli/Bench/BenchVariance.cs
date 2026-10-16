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
        sb.AppendLine("Only repetitions covering every prepared entry with no errors, degraded councils, or skipped entries are included. Missing samples are never imputed. Sample variance uses n−1 and rates on the 0–1 scale; SD is in percentage points. Fewer than two complete repetitions gives n/a variance. Replayed answers must be independent model responses to support sampling claims.");
        sb.AppendLine();
        sb.AppendLine("| Arm | Metric | Complete repetitions | Mean | Sample variance | Sample SD | Min–max |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var arm in Enum.GetValues<BenchArm>())
        {
            var complete = results.Rows.Where(r => r.Arm == arm)
                .GroupBy(r => r.Repetition)
                .Where(g => results.Skipped.Count == 0 && results.Metadata.EntryHeads.Count > 0
                    && g.Count() == results.Metadata.EntryHeads.Count
                    && g.Select(r => r.EntryId).ToHashSet(StringComparer.Ordinal).SetEquals(results.Metadata.EntryHeads.Keys)
                    && g.All(r => r.Error is null && !r.Degraded && r.Score is not null))
                .Select(g => g.Select(r => r.Score!).ToArray()).ToArray();
            if (!results.Rows.Any(r => r.Arm == arm)) continue;
            AddMetric(sb, arm, "Pooled invented rate", complete.Select(scores =>
            {
                var citations = scores.Sum(s => s.Citations);
                return citations == 0 ? (double?)null : (double)scores.Sum(s => s.Invented) / citations;
            }));
            AddMetric(sb, arm, "Mean precision", complete.Select(scores => Average(scores.Select(s => s.Precision))));
            AddMetric(sb, arm, "Mean recall", complete.Select(scores => Average(scores.Select(s => s.Recall))));
        }
    }

    private static double? Average(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return present.Length == 0 ? null : present.Average();
    }

    private static void AddMetric(StringBuilder sb, BenchArm arm, string metric, IEnumerable<double?> values)
    {
        var samples = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var mean = samples.Length == 0 ? (double?)null : samples.Average();
        var variance = samples.Length < 2 ? (double?)null
            : samples.Sum(v => Math.Pow(v - mean!.Value, 2)) / (samples.Length - 1);
        string Pct(double? v) => v?.ToString("0.0%", CultureInfo.InvariantCulture) ?? "n/a";
        var sd = variance is null ? "n/a" : (100 * Math.Sqrt(variance.Value)).ToString("0.0", CultureInfo.InvariantCulture) + " pp";
        var range = samples.Length == 0 ? "n/a" : $"{Pct(samples.Min())}–{Pct(samples.Max())}";
        sb.AppendLine($"| {arm} | {metric} | {samples.Length} | {Pct(mean)} | {variance?.ToString("0.######", CultureInfo.InvariantCulture) ?? "n/a"} | {sd} | {range} |");
    }
}
