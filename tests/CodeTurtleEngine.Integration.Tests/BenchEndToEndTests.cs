using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Integration.Tests;

public class BenchEndToEndTests
{
    // Naive prompts cite an invented member; grounded prompts cite a real, payload-resolved type
    // (string.Format in Describe resolves "string" via ToDisplayString; Mini.Calc itself is not in the allow-list).
    private sealed class ModeAwareChat : IStructuredChatClient
    {
        public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages, string jsonSchema, CancellationToken ct = default)
        {
            var naive = messages[0].Text.Contains("full source of the changed", StringComparison.Ordinal);
            var cite = naive ? "Mini.Calc.FormatTotalFast" : "string";
            var dto = new VerdictDto(new[] { new FindingDto(Severity.Warning, "Boxing", "decimal boxed by string.Format", "Calc.cs:7", new[] { cite }) });
            return Task.FromResult((T)(object)dto);
        }
    }

    [Fact]
    public async Task Bench_Runs_Seeded_Corpus_Offline()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-bench-e2e-" + Guid.NewGuid().ToString("N"));
        var rubric = Path.Combine(root, "rubric.md");
        Directory.CreateDirectory(root);
        File.WriteAllText(rubric, "# Rubric");
        var corpus = CorpusLoader.Load(Path.Combine(TestPaths.RepoRoot, "tests", "fixtures", "BenchCorpus", "corpus.json"));

        ReviewPipeline PipelineFor(PromptMode mode)
        {
            var options = new TurtleOptions { RubricPath = rubric, Council = new CouncilOptions { PromptMode = mode, Guard = GuardMode.Flag } };
            return new ReviewPipeline(new GitDiffProvider(), new MsBuildCompilationLoader(), new PersonaRunner(new ModeAwareChat(), options.Council),
                new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(Path.Combine(root, "home")), options);
        }

        var preparer = new CorpusPreparer(Path.Combine(root, "work"), DotnetRestore.RunAsync);
        var results = await new BenchRunner(PipelineFor, new MsBuildCompilationLoader(), preparer.PrepareAsync, TextWriter.Null)
            .RunAsync(corpus, repeat: 1, Array.Empty<string>(), default);

        Assert.Empty(results.Skipped);
        var boxingNaive = results.Rows.Single(r => r.EntryId == "boxing" && r.Arm == BenchArm.Naive).Score!;
        // Arbiter dedupes by (title, location): 3 identical persona findings merge into 1, and the
        // first in persona order (AllocationsPerformance) is kept.
        Assert.Equal(1, boxingNaive.Invented);
        var boxingTurtle = results.Rows.Single(r => r.EntryId == "boxing" && r.Arm == BenchArm.CodeTurtle).Score!;
        Assert.Equal(0, boxingTurtle.Invented);
        Assert.Equal(1, boxingTurtle.Matched);  // AllocationsPerformance finding at Calc.cs:7
        var clean = results.Rows.Single(r => r.EntryId == "clean" && r.Arm == BenchArm.CodeTurtle).Score!;
        Assert.Equal(0, clean.Matched);
        Assert.Null(clean.Recall);              // no expected defects

        var (_, md) = BenchReport.Write(results, Path.Combine(root, "out"));
        Assert.Contains("## Summary by arm", File.ReadAllText(md));

        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(root, true);
    }
}
