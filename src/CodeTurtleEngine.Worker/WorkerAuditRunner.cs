using System.Runtime.CompilerServices;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

[assembly: InternalsVisibleTo("CodeTurtleEngine.Integration.Tests")]

namespace CodeTurtleEngine.Worker;

internal sealed record WorkerAuditResult(
    string ArchiveSha256,
    GuardOutcome Outcome,
    string Markdown,
    IReadOnlyList<string> Diagnostics);

internal static class WorkerAuditRunner
{
    public static WorkerAuditResult ExecuteAudit(
        WorkerRequest request,
        LoadedCompilation loaded,
        string hash,
        CouncilOptions? options = null)
    {
        if (request.Findings.Any(f => f.CitedSymbolFqns is null || f.Title is null || f.Detail is null || f.Location is null))
            throw new InvalidDataException();

        var payload = new PayloadGenerator(loaded.Projects).Build("sealed-archive", hash, request.ChangedFiles, request.DiffHunks);
        var oracle = new SymbolOracle(loaded.Projects);
        var merged = new Arbiter().Merge(request.Findings.GroupBy(f => f.Persona).Select(g => new PersonaVerdict(g.Key, g.ToList())).ToList());
        var guardOptions = options ?? new CouncilOptions { Guard = request.Guard, RequireCitationsInStripMode = request.Strict };
        var guarded = new TurtleShellGuard(guardOptions).Verify(merged, payload, citationResolver: oracle.Resolve);

        var diagnostics = new List<string>(loaded.Diagnostics);
        if (request.DiffHunks is null)
            diagnostics.Add(DiffEvidence.UnavailableDiagnostic);

        var markdown = new Arbiter().RenderMarkdown(guarded.KeptFindings, payload);
        if (diagnostics.Count > 0)
            markdown = "> Compilation/workspace diagnostics (review permitted with warnings):\n> " +
                string.Join("\n> ", diagnostics) + "\n\n" + markdown;

        return new WorkerAuditResult(hash, guarded, markdown, diagnostics);
    }
}
