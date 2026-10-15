using System.Diagnostics;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Cli.Bench;

public enum BenchArm { Naive, Grounded, CodeTurtle }

public sealed record BenchRow(string EntryId, BenchArm Arm, int Repetition, RunScore? Score, long ElapsedMs, int PersonasSucceeded, string? Error)
{
    private const int ReviewPersonas = 3; // matches PersonaRunner's review personas
    public bool Degraded => Error is null && PersonasSucceeded < ReviewPersonas;
}

public sealed record SkippedEntry(string EntryId, string Reason);

public sealed record BenchMetadata(DateTimeOffset StartedUtc, int Repeat, IReadOnlyList<string> Routes, IReadOnlyDictionary<string, string> EntryHeads);

public sealed record BenchResults(BenchMetadata Metadata, IReadOnlyList<BenchRow> Rows, IReadOnlyList<SkippedEntry> Skipped);

public sealed class BenchRunner
{
    private static readonly TurtleShellGuard StripGuard = new(new CouncilOptions { Guard = GuardMode.Strip });

    private readonly Func<PromptMode, ReviewPipeline> _pipelineFor;
    private readonly ICompilationLoader _loader;
    private readonly Func<CorpusEntry, CancellationToken, Task<PreparedEntry>> _prepare;
    private readonly TextWriter _log;

    /// <param name="pipelineFor">Must return pipelines configured with GuardMode.Flag.</param>
    public BenchRunner(Func<PromptMode, ReviewPipeline> pipelineFor, ICompilationLoader loader,
        Func<CorpusEntry, CancellationToken, Task<PreparedEntry>> prepare, TextWriter log)
    {
        _pipelineFor = pipelineFor; _loader = loader; _prepare = prepare; _log = log;
    }

    public async Task<BenchResults> RunAsync(CorpusManifest corpus, int repeat, IReadOnlyList<string> routes, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var rows = new List<BenchRow>();
        var skipped = new List<SkippedEntry>();
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in corpus.Entries)
        {
            PreparedEntry prepared;
            LoadedCompilation loaded;
            try
            {
                prepared = await _prepare(entry, ct).ConfigureAwait(false);
                var unknown = prepared.Expected?.FirstOrDefault(d => !corpus.ClassPersonas.ContainsKey(d.Class));
                if (unknown is not null)
                    throw new BenchException($"{entry.Id}: defect class '{unknown.Class}' missing from classPersonas.");
                loaded = await _loader.LoadAsync(prepared.ProjectPath, ct).ConfigureAwait(false);
                prepared = prepared with { Expected = ExpectedDefectEnricher.Enrich(loaded.Compilation, prepared.Expected) };
            }
            catch (Exception ex) when (ex is BenchException or CompilationException or DiffException
                or LibGit2Sharp.LibGit2SharpException or IOException)
            {
                skipped.Add(new SkippedEntry(entry.Id, ex.Message));
                _log.WriteLine($"[bench] skip {entry.Id}: {ex.Message}");
                continue;
            }

            heads[entry.Id] = prepared.HeadSha;
            try
            {
                var oracle = new SymbolOracle(loaded.Compilation);
                for (var rep = 1; rep <= repeat; rep++)
                {
                    foreach (var mode in new[] { PromptMode.Naive, PromptMode.Grounded })
                    {
                        _log.WriteLine($"[bench] {entry.Id} {mode} rep {rep}/{repeat}");
                        rows.AddRange(await RunArmAsync(entry, prepared, mode, rep, oracle, corpus.ClassPersonas, ct).ConfigureAwait(false));
                    }
                }
            }
            finally
            {
                loaded.Workspace.Dispose();
            }
        }

        return new BenchResults(new BenchMetadata(started, repeat, routes, heads), rows, skipped);
    }

    private async Task<IReadOnlyList<BenchRow>> RunArmAsync(CorpusEntry entry, PreparedEntry prepared, PromptMode mode, int rep,
        SymbolOracle oracle, IReadOnlyDictionary<string, PersonaRole> classPersonas, CancellationToken ct)
    {
        var arm = mode == PromptMode.Naive ? BenchArm.Naive : BenchArm.Grounded;
        var sw = Stopwatch.StartNew();
        ReviewResult result;
        try
        {
            result = await _pipelineFor(mode).RunAsync(prepared.RepoDir, prepared.ProjectPath, prepared.BaselineSha, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ProviderException or CompilationException or DiffException)
        {
            _log.WriteLine($"[bench] {entry.Id} {mode} rep {rep} failed: {ex.Message}");
            var failed = new List<BenchRow> { new(entry.Id, arm, rep, null, sw.ElapsedMilliseconds, 0, ex.Message) };
            if (mode == PromptMode.Grounded)
                failed.Add(new BenchRow(entry.Id, BenchArm.CodeTurtle, rep, null, sw.ElapsedMilliseconds, 0, ex.Message));
            return failed;
        }
        sw.Stop();

        var allow = TurtleShellGuard.BuildAllowList(result.Payload);
        var personas = result.Verdict.Personas.Count;
        RunScore Score(IReadOnlyList<ReviewFinding> findings) =>
            BenchScorer.Score(findings, allow, oracle.Exists, prepared.Expected, classPersonas);

        var rows = new List<BenchRow> { new(entry.Id, arm, rep, Score(result.Verdict.Synthesized), sw.ElapsedMilliseconds, personas, null) };
        if (mode == PromptMode.Grounded)
        {
            var stripped = StripGuard.Verify(result.Verdict.Synthesized, result.Payload).KeptFindings;
            rows.Add(new BenchRow(entry.Id, BenchArm.CodeTurtle, rep, Score(stripped), sw.ElapsedMilliseconds, personas, null));
        }
        return rows;
    }
}
