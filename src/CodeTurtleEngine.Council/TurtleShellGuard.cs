using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record GuardOutcome(
    IReadOnlyList<ReviewFinding> KeptFindings,
    IReadOnlyList<GuardResult> Audit)
{
    public IReadOnlyList<FindingAudit> FindingAudit { get; init; } = Array.Empty<FindingAudit>();
}

public interface ITurtleShellGuard
{
    GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, Func<string, bool>? compilationOracle = null,
        Func<string, CitationResolution>? citationResolver = null);
}

public sealed class TurtleShellGuard : ITurtleShellGuard
{
    private readonly CouncilOptions _options;

    public TurtleShellGuard(CouncilOptions options) => _options = options;

    public static HashSet<string> BuildAllowList(RoslynPayload payload)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in payload.Files)
        {
            foreach (var m in file.Methods)
            {
                foreach (var sym in m.ResolvedSymbols)
                {
                    set.Add(sym);
                }
            }
        }
        return set;
    }

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload) =>
        Verify(findings, payload, null);

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, Func<string, bool>? compilationOracle = null,
        Func<string, CitationResolution>? citationResolver = null)
    {
        var allow = BuildAllowList(payload);

        var audit = new List<GuardResult>();
        var kept = new List<ReviewFinding>();

        foreach (var finding in findings)
        {
            var verified = new List<string>();
            foreach (var cite in finding.CitedSymbolFqns)
            {
                var inPayload = allow.Contains(cite);
                var resolution = citationResolver?.Invoke(cite);
                var inCompilation = resolution?.Exists ?? compilationOracle?.Invoke(cite);
                // When a compilation oracle is present it is authoritative, even for payload names.
                var ok = inCompilation ?? inPayload;
                var evidence = !ok ? "Rejected" : inPayload ? "Payload" : "Compilation";
                audit.Add(new GuardResult(cite, ok, evidence)
                {
                    Resolution = resolution,
                    PayloadContexts = CitationContext.Find(payload, cite)
                });
                if (ok) verified.Add(cite);
            }

            if (_options.Guard == GuardMode.Flag)
            {
                kept.Add(finding);
                continue;
            }

            // Strip mode:
            if (_options.RequireCitationsInStripMode && finding.CitedSymbolFqns.Count == 0)
            {
                continue;
            }

            if (finding.CitedSymbolFqns.Count > 0 && verified.Count == 0)
            {
                continue;
            }

            kept.Add(finding with { CitedSymbolFqns = verified });
        }

        var findingAudit = findings.Select(f =>
        {
            var retained = kept.Any(k => k.Persona == f.Persona && k.Title == f.Title && k.Location == f.Location);
            var rejected = f.CitedSymbolFqns.Any(c => audit.Any(a => a.CitedFqn == c && !a.Verified));
            var status = f.CitedSymbolFqns.Count == 0 ? "UncitedAdvisory" : rejected ? "RejectedCitations" : "CitationExistenceOnly";
            return new FindingAudit(f, retained, status);
        }).ToList();
        return new GuardOutcome(kept, audit) { FindingAudit = findingAudit };
    }
}
