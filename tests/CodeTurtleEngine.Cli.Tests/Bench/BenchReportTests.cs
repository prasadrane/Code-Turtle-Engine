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
            new BenchRow("e2", BenchArm.Naive, 1, null, 50, 0, "quorum"),
            new BenchRow("e4", BenchArm.Naive, 1, new RunScore(0, 0, 0, 0, 0, 0, 1), 10, 3, null)
        },
        new[] { new SkippedEntry("e3", "compile failed") });

    [Fact]
    public void Markdown_Summarizes_Each_Arm_Errors_Skips_And_Limits()
    {
        var md = BenchReport.RenderMarkdown(Results);

        Assert.Contains("| Naive | 3 | 1 | 0 |", md);       // runs, errors, degraded
        Assert.Contains("30.0%", md);                       // naive invented rate 3/10
        Assert.Contains("| Grounded | 1 | 0 | 1 |", md);    // 2/3 personas -> degraded
        Assert.Contains("e3", md);
        Assert.Contains("compile failed", md);
        Assert.Contains("## Limits", md);
        Assert.Contains("keeps the first persona", md);
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

    [Fact]
    public void Zero_Citation_Rows_Do_Not_Dilute_Mean_And_Pooled_Rate_Is_Shown()
    {
        var md = BenchReport.RenderMarkdown(Results);
        Assert.Contains("Pooled invented rate", md);
        var line = md.Split('\n').First(l => l.StartsWith("| Naive | 3", StringComparison.Ordinal));
        Assert.Contains("30.0% (30.0%", line);
    }

    [Fact]
    public void All_Zero_Citation_Rows_Show_Na()
    {
        var r = new BenchResults(Results.Metadata,
            new[] { new BenchRow("e", BenchArm.Naive, 1, new RunScore(0, 0, 0, 0, 0, null, null), 5, 3, null) },
            Array.Empty<SkippedEntry>());
        var line = BenchReport.RenderMarkdown(r).Split('\n').First(l => l.StartsWith("| Naive", StringComparison.Ordinal));
        Assert.Contains("n/a (n/a", line);
    }

    [Fact]
    public void Summary_Reports_Pooled_And_Mean_Precision_And_Recall_With_Denominators()
    {
        var r = new BenchResults(
            new BenchMetadata(DateTimeOffset.UnixEpoch, 1, new[] { "route:model" },
                new Dictionary<string, string> { ["e1"] = "a", ["e2"] = "b", ["clean"] = "c" }),
            new[]
            {
                new BenchRow("e1", BenchArm.Grounded, 1, new RunScore(4, 4, 0, 0, 0, 1, 1), 10, 3, null),
                new BenchRow("e2", BenchArm.Grounded, 1, new RunScore(0, 0, 0, 0, 0, 0, 1), 10, 3, null),
                new BenchRow("clean", BenchArm.Grounded, 1, new RunScore(1, 1, 0, 0, 0, 0, 0), 10, 3, null)
            },
            Array.Empty<SkippedEntry>());

        var md = BenchReport.RenderMarkdown(r);
        // Pooled precision: 1 matched across 5 findings (20.0% (1/5)); mean precision: only e1 and clean have findings > 0 ((25% + 0%)/2 = 12.5% (2/3))
        Assert.Contains("20.0% (1/5)", md);
        Assert.Contains("12.5% (2/3)", md);
        // Pooled recall: 1 matched across 2 expected defects (50.0% (1/2)); mean recall: 2 entries with expected > 0 (50.0% (2/3))
        Assert.Contains("50.0% (1/2)", md);
        Assert.Contains("50.0% (2/3)", md);
    }

    [Fact]
    public void Variance_Requires_Cross_Arm_And_Entry_Set_Completeness_Without_Degraded_Or_Skipped_Runs()
    {
        BenchRow[] Rep(int rep, int naiveInv, int groundedFindings = 1, int personas = 3, string? groundedErr = null) =>
            new[]
            {
                new BenchRow("e1", BenchArm.Naive, rep, new RunScore(1, 2, 0, naiveInv, 0, 1, 1), 1, 3, null),
                new BenchRow("e2", BenchArm.Naive, rep, new RunScore(1, 2, 0, naiveInv, 0, 1, 1), 1, 3, null),
                new BenchRow("e1", BenchArm.Grounded, rep, groundedErr is null ? new RunScore(groundedFindings, 2, 0, 0, 0, 1, 1) : null, 1, personas, groundedErr),
                new BenchRow("e2", BenchArm.Grounded, rep, new RunScore(1, 2, 0, 0, 0, 1, 1), 1, 3, null),
                new BenchRow("e1", BenchArm.CodeTurtle, rep, new RunScore(1, 2, 0, 0, 0, 1, 1), 1, 3, null),
                new BenchRow("e2", BenchArm.CodeTurtle, rep, new RunScore(1, 2, 0, 0, 0, 1, 1), 1, 3, null)
            };

        var meta = new BenchMetadata(DateTimeOffset.UnixEpoch, 3, new[] { "route:model" },
            new Dictionary<string, string> { ["e1"] = "a", ["e2"] = "b" });
        var fullRows = Rep(1, 0).Concat(Rep(2, 1)).Concat(Rep(3, 2)).ToArray();
        var r = new BenchResults(meta, fullRows, Array.Empty<SkippedEntry>());

        var md = BenchReport.RenderMarkdown(r);
        Assert.Contains("## Variation across repetitions", md);
        Assert.Contains("| Naive | Pooled invented rate | 3 | 3 | 50.0% | 0.25 | 50.0 pp | 0.0%–100.0% |", md);

        // If Grounded fails in rep 3, rep 3 is excluded for ALL arms (including Naive) so arms compare identical repetition sets.
        var groundedFailedRep3 = Rep(1, 0).Concat(Rep(2, 1)).Concat(Rep(3, 2, groundedErr: "quorum")).ToArray();
        Assert.Contains("| Naive | Pooled invented rate | 2 | 2 |", BenchReport.RenderMarkdown(r with { Rows = groundedFailedRep3 }));

        // If Grounded is degraded (2/3 personas) in rep 2, rep 2 is excluded across all arms.
        var degradedRep2 = Rep(1, 0).Concat(Rep(2, 1, personas: 2)).Concat(Rep(3, 2)).ToArray();
        Assert.Contains("| Naive | Pooled invented rate | 2 | 2 |", BenchReport.RenderMarkdown(r with { Rows = degradedRep2 }));

        // If an entry in EntryHeads is missing from a repetition, that repetition is excluded.
        var missingEntry = fullRows.Where(x => !(x.Repetition == 3 && x.EntryId == "e2")).ToArray();
        Assert.Contains("| Naive | Pooled invented rate | 2 | 2 |", BenchReport.RenderMarkdown(r with { Rows = missingEntry }));

        // Any skipped entry in the run invalidates corpus-complete repetition variance.
        var withSkip = r with { Skipped = new[] { new SkippedEntry("e3", "compile failed") } };
        Assert.Contains("| Naive | Pooled invented rate | 0 | 0 | n/a | n/a | n/a | n/a |", BenchReport.RenderMarkdown(withSkip));
    }

    [Fact]
    public void Variance_Tracks_Defined_Samples_Separately_From_Complete_Repetitions()
    {
        // Rep 1 has 0 citations on Grounded (Pooled invented rate is null), Rep 2 and Rep 3 have citations > 0.
        BenchRow[] Rep(int rep, int cites) =>
            new[]
            {
                new BenchRow("e1", BenchArm.Naive, rep, new RunScore(1, 2, 0, 0, 0, 1, 1), 1, 3, null),
                new BenchRow("e1", BenchArm.Grounded, rep, new RunScore(1, cites, 0, 0, 0, 1, 1), 1, 3, null),
                new BenchRow("e1", BenchArm.CodeTurtle, rep, new RunScore(1, cites, 0, 0, 0, 1, 1), 1, 3, null)
            };

        var r = new BenchResults(
            new BenchMetadata(DateTimeOffset.UnixEpoch, 3, new[] { "route:model" }, new Dictionary<string, string> { ["e1"] = "a" }),
            Rep(1, 0).Concat(Rep(2, 2)).Concat(Rep(3, 2)).ToArray(),
            Array.Empty<SkippedEntry>());

        var md = BenchReport.RenderMarkdown(r);
        // 3 complete repetitions, 2 defined samples for Grounded Pooled invented rate
        Assert.Contains("| Grounded | Pooled invented rate | 3 | 2 | 0.0% | 0 | 0.0 pp | 0.0%–0.0% |", md);
    }

    [Fact]
    public void Single_Repetition_Has_No_Variance_And_Replay_Has_No_Model_Latency()
    {
        var r = new BenchResults(
            new BenchMetadata(DateTimeOffset.UnixEpoch, 1, new[] { "replay:prompts" }, new Dictionary<string, string> { ["e1"] = "abc" }),
            new[]
            {
                new BenchRow("e1", BenchArm.Naive, 1, new RunScore(4, 10, 6, 3, 2, 1, 2), 1000, 3, null),
                new BenchRow("e1", BenchArm.Grounded, 1, new RunScore(4, 8, 0, 0, 0, 2, 2), 2000, 3, null),
                new BenchRow("e1", BenchArm.CodeTurtle, 1, new RunScore(4, 8, 0, 0, 0, 2, 2), 2000, 3, null)
            },
            Array.Empty<SkippedEntry>());
        var md = BenchReport.RenderMarkdown(r);
        Assert.Contains("n/a (replay)", md);
        Assert.Contains("independent model latency", md);
        Assert.Contains("| Naive | Pooled invented rate | 1 | 1 | 30.0% | n/a | n/a | 30.0%–30.0% |", md);
    }
}
