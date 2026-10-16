using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record GuardOutcome(
    IReadOnlyList<ReviewFinding> KeptFindings,
    IReadOnlyList<GuardResult> Audit);

public interface ITurtleShellGuard
{
    GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, Func<string, bool>? compilationOracle = null);
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
                    var lastDot = sym.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        var prefix = sym.Substring(0, lastDot);
                        if (prefix.Contains('.'))
                        {
                            set.Add(prefix);
                        }
                    }
                }
            }
        }
        return set;
    }

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload) =>
        Verify(findings, payload, null);

    public GuardOutcome Verify(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, Func<string, bool>? compilationOracle = null)
    {
        var allow = BuildAllowList(payload);

        var audit = new List<GuardResult>();
        var kept = new List<ReviewFinding>();

        foreach (var finding in findings)
        {
            var verified = new List<string>();
            foreach (var cite in finding.CitedSymbolFqns)
            {
                var ok = allow.Contains(cite) || (compilationOracle is not null && compilationOracle(cite));
                audit.Add(new GuardResult(cite, ok));
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

        return new GuardOutcome(kept, audit);
    }
}
