using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record GuardOutcome(
    IReadOnlyList<ReviewFinding> KeptFindings,
    IReadOnlyList<GuardResult> Audit);

public interface ITurtleShellGuard
{
    GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload);
}

public sealed class TurtleShellGuard : ITurtleShellGuard
{
    private readonly CouncilOptions _options;

    public TurtleShellGuard(CouncilOptions options) => _options = options;

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload)
    {
        var allow = payload.Files
            .SelectMany(f => f.Methods)
            .SelectMany(m => m.ResolvedSymbols)
            .ToHashSet(StringComparer.Ordinal);

        var audit = new List<GuardResult>();
        var kept = new List<ReviewFinding>();

        foreach (var finding in findings)
        {
            var verified = new List<string>();
            foreach (var cite in finding.CitedSymbolFqns)
            {
                var ok = allow.Contains(cite);
                audit.Add(new GuardResult(cite, ok));
                if (ok) verified.Add(cite);
            }

            if (_options.Guard == GuardMode.Flag)
            {
                kept.Add(finding);
                continue;
            }

            // Strip mode: drop a finding that cited symbols but none verified.
            if (finding.CitedSymbolFqns.Count > 0 && verified.Count == 0) continue;
            kept.Add(finding with { CitedSymbolFqns = verified });
        }

        return new GuardOutcome(kept, audit);
    }
}
