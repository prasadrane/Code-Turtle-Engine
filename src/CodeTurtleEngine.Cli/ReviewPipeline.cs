using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Cli;

public sealed record ReviewResult(string Markdown, CouncilVerdict Verdict, string ArtifactDir, RoslynPayload Payload);

public sealed class ReviewPipeline
{
    private readonly IDiffProvider _diff;
    private readonly ICompilationLoader _loader;
    private readonly IPersonaRunner _personas;
    private readonly IArbiter _arbiter;
    private readonly ITurtleShellGuard _guard;
    private readonly RubricLoader _rubricLoader;
    private readonly ArtifactWriter _artifacts;
    private readonly TurtleOptions _options;

    public ReviewPipeline(IDiffProvider diff, ICompilationLoader loader, IPersonaRunner personas,
        IArbiter arbiter, ITurtleShellGuard guard, RubricLoader rubricLoader, ArtifactWriter artifacts, TurtleOptions options)
    {
        _diff = diff; _loader = loader; _personas = personas; _arbiter = arbiter;
        _guard = guard; _rubricLoader = rubricLoader; _artifacts = artifacts; _options = options;
    }

    public async Task<ReviewResult> RunAsync(string repoPath, string projectPath, string? baselineRef, CancellationToken ct = default)
    {
        var changed = _diff.GetChangedCSharpFiles(repoPath, baselineRef);
        var loaded = await _loader.LoadAsync(projectPath, ct).ConfigureAwait(false);
        try
        {
            var repoSlug = Path.GetFileNameWithoutExtension(projectPath);
            var payload = new PayloadGenerator(loaded.Projects).Build(repoSlug, baselineRef ?? "working-tree", changed);
            var rubric = _rubricLoader.Load(ResolveRubric());
            // THE INVARIANT: trimming applies ONLY to the LLM prompt projection. The guard
            // must keep verifying against the FULL payload's ResolvedSymbols allow-list.
            var promptPayload = PayloadTrimmer.TrimForPrompt(payload, _options.Council);
            var sources = _options.Council.PromptMode == PromptMode.Naive
                ? CollectSources(loaded.Projects, changed, _options.Council.MaxFiles)
                : Array.Empty<SourceFile>();
            var verdicts = await _personas.RunAsync(promptPayload, sources, rubric, ct).ConfigureAwait(false);
            var merged = _arbiter.Merge(verdicts);
            var guarded = _guard.Verify(merged, payload, citationResolver: new SymbolOracle(loaded.Projects).Resolve);
            var markdown = _arbiter.RenderMarkdown(guarded.KeptFindings, payload);
            const int ExpectedReviewPersonas = 3; // matches PersonaRunner's review personas
            if (verdicts.Count < ExpectedReviewPersonas)
                markdown = $"> Degraded review: {verdicts.Count}/{ExpectedReviewPersonas} council personas succeeded.\n\n" + markdown;
            if (loaded.Diagnostics.Count > 0)
                markdown = "> Compilation/workspace diagnostics (review permitted with warnings):\n> " +
                    string.Join("\n> ", loaded.Diagnostics) + "\n\n" + markdown;
            var verdict = new CouncilVerdict(verdicts, guarded.KeptFindings, guarded.Audit) { FindingAudit = guarded.FindingAudit };
            var dir = _artifacts.Write(repoSlug, payload, verdict, markdown, loaded.Diagnostics);
            return new ReviewResult(markdown, verdict, dir, payload);
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }

    private static IReadOnlyList<SourceFile> CollectSources(IReadOnlyList<Compilation> compilations, IReadOnlyList<string> changed, int maxFiles) =>
        changed
            .Select(rel => (rel, tree: compilations.SelectMany(c => c.SyntaxTrees).FirstOrDefault(t => RepoPaths.EndsWith(t.FilePath, rel))))
            .Where(x => x.tree is not null)
            .Take(maxFiles)
            .Select(x => new SourceFile(x.rel, x.tree!.GetText().ToString()))
            .ToList();

    private string ResolveRubric() =>
        Path.IsPathRooted(_options.RubricPath)
            ? _options.RubricPath
            : Path.Combine(Directory.GetCurrentDirectory(), _options.RubricPath);
}
