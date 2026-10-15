using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CodeTurtleEngine.Cli.Bench;

public static class BenchReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static (string JsonPath, string MarkdownPath) Write(BenchResults results, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var json = Path.Combine(outDir, "bench-results.json");
        var md = Path.Combine(outDir, "bench-report.md");
        File.WriteAllText(json, JsonSerializer.Serialize(results, BenchJson.Options));
        File.WriteAllText(md, RenderMarkdown(results));
        return (json, md);
    }

    public static string RenderMarkdown(BenchResults r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Code Turtle Benchmark");
        sb.AppendLine();
        sb.AppendLine(Inv, $"Started: {r.Metadata.StartedUtc:u} · Repetitions: {r.Metadata.Repeat} · Routes: {string.Join(", ", r.Metadata.Routes)}");
        sb.AppendLine();
        sb.AppendLine("## Summary by arm");
        sb.AppendLine();
        sb.AppendLine("| Arm | Runs | Errors | Degraded | Citations | Invented rate (mean, min–max) | Pooled invented rate | Unverified rate (mean) | Findings w/ invented | Precision (mean) | Recall (mean) | Median latency |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var arm in Enum.GetValues<BenchArm>())
        {
            var rows = r.Rows.Where(x => x.Arm == arm).ToList();
            if (rows.Count == 0) continue;
            var scored = rows.Where(x => x.Score is not null).Select(x => x.Score!).ToList();
            var invented = scored.Where(s => s.InventedRate is not null).Select(s => s.InventedRate!.Value).ToList();
            var totalCites = scored.Sum(s => s.Citations);
            double? pooled = totalCites == 0 ? null : (double)scored.Sum(s => s.Invented) / totalCites;
            sb.AppendLine(Inv,
                $"| {arm} | {rows.Count} | {rows.Count(x => x.Error is not null)} | {rows.Count(x => x.Degraded)} | {scored.Sum(s => s.Citations)} | " +
                $"{Pct(Mean(invented))} ({(invented.Count == 0 ? "n/a" : Pct(invented.Min()))}–{(invented.Count == 0 ? "n/a" : Pct(invented.Max()))}) | " +
                $"{Pct(pooled)} | " +
                $"{Pct(Mean(scored.Where(s => s.UnverifiedRate is not null).Select(s => s.UnverifiedRate!.Value)))} | {scored.Sum(s => s.FindingsWithInvented)} | " +
                $"{Pct(Mean(scored.Where(s => s.Precision is not null).Select(s => s.Precision!.Value)))} | " +
                $"{Pct(Mean(scored.Where(s => s.Recall is not null).Select(s => s.Recall!.Value)))} | " +
                $"{Median(rows.Where(x => x.Error is null).Select(x => x.ElapsedMs))} ms |");
        }

        sb.AppendLine();
        sb.AppendLine("## Per entry");
        sb.AppendLine();
        sb.AppendLine("| Entry | Arm | Rep | Findings | Citations | Invented | Unverified | Matched/Expected | Error |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var x in r.Rows)
        {
            var s = x.Score;
            sb.AppendLine(Inv,
                $"| {x.EntryId} | {x.Arm} | {x.Repetition} | {s?.Findings} | {s?.Citations} | {s?.Invented} | {s?.Unverified} | " +
                $"{(s?.Matched is null ? "" : $"{s.Matched}/{s.ExpectedCount}")} | {x.Error} |");
        }

        if (r.Skipped.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Skipped entries");
            sb.AppendLine();
            foreach (var s in r.Skipped) sb.AppendLine($"- `{s.EntryId}`: {s.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("## Limits");
        sb.AppendLine();
        sb.AppendLine("- Invented = cited name resolves to no type or member in the compilation (SymbolOracle). Unverified = not in the guard allow-list, which holds only types the changed methods touch.");
        sb.AppendLine("- Small corpus; seeded defects are authored by this project; a single model family per run.");
        sb.AppendLine("- The guard covers the citation channel only; prose and locations are not verified.");
        sb.AppendLine("- Precision/recall use deterministic matching (same file and either line ±3 or the enclosing method named; persona mapped from defect class); valid findings outside the labels count as false positives.");
        sb.AppendLine("- The arbiter merges findings by (title, location) and keeps the first persona, so a defect mapped to a dropped persona can be undercounted.");
        return sb.ToString();
    }

    private static double? Mean(IEnumerable<double> xs)
    {
        var list = xs.ToList();
        return list.Count == 0 ? null : list.Average();
    }

    private static string Pct(double? v) => v is null ? "n/a" : v.Value.ToString("0.0%", Inv);

    private static string Median(IEnumerable<long> xs)
    {
        var s = xs.OrderBy(x => x).ToList();
        return s.Count == 0 ? "n/a" : s[s.Count / 2].ToString(Inv);
    }
}
