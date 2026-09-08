using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli;

public sealed record ReviewResult(string Markdown, CouncilVerdict Verdict, string ArtifactDir);

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
        var loaded = await _loader.LoadAsync(projectPath, ct);
        try
        {
            var repoSlug = Path.GetFileNameWithoutExtension(projectPath);
            var payload = new PayloadGenerator(loaded.Compilation).Build(repoSlug, baselineRef ?? "working-tree", changed);
            var rubric = _rubricLoader.Load(ResolveRubric());
            var verdicts = await _personas.RunAsync(payload, rubric, ct);
            var merged = _arbiter.Merge(verdicts);
            var guarded = _guard.Verify(merged, payload);
            var markdown = _arbiter.RenderMarkdown(guarded.KeptFindings, payload);
            const int ExpectedReviewPersonas = 3; // matches PersonaRunner's review personas
            if (verdicts.Count < ExpectedReviewPersonas)
                markdown = $"> Degraded review: {verdicts.Count}/{ExpectedReviewPersonas} council personas succeeded.\n\n" + markdown;
            var verdict = new CouncilVerdict(verdicts, guarded.KeptFindings, guarded.Audit);
            var dir = _artifacts.Write(repoSlug, payload, verdict, markdown);
            return new ReviewResult(markdown, verdict, dir);
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }

    private string ResolveRubric() =>
        Path.IsPathRooted(_options.RubricPath)
            ? _options.RubricPath
            : Path.Combine(Directory.GetCurrentDirectory(), _options.RubricPath);
}
