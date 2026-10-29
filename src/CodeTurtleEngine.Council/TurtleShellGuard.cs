using System.Text.RegularExpressions;
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
        Func<string, CitationResolution>? citationResolver = null, Func<string, int, int, string?>? sourceResolver = null);
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
        Func<string, CitationResolution>? citationResolver = null, Func<string, int, int, string?>? sourceResolver = null)
    {
        var allow = BuildAllowList(payload);

        var audit = new List<GuardResult>();
        var kept = new List<ReviewFinding>();
        var findingStatuses = new Dictionary<ReviewFinding, string>();

        foreach (var finding in findings)
        {
            // Always audit citations (L0 invariant: every citation must be recorded in audit)
            var verified = new List<string>();
            foreach (var cite in finding.CitedSymbolFqns)
            {
                var inPayload = allow.Contains(cite);
                var resolution = citationResolver?.Invoke(cite);
                var inCompilation = resolution?.Exists ?? compilationOracle?.Invoke(cite);
                var ok = inCompilation ?? inPayload;
                var evidence = !ok ? "Rejected" : inPayload ? "Payload" : "Compilation";
                audit.Add(new GuardResult(cite, ok, evidence)
                {
                    Resolution = resolution,
                    PayloadContexts = CitationContext.Find(payload, cite)
                });
                if (ok) verified.Add(cite);
            }

            // L1: Hunk & Location intersection
            bool locationFailed = false;
            if (_options.EnableHunkIntersectionGuard && payload.Files.Count > 0)
            {
                var (file, startLine, endLine) = ParseLocation(finding.Location);
                var matchedFile = payload.Files.FirstOrDefault(f =>
                    f.FilePath.EndsWith(file, StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(f.FilePath, StringComparison.OrdinalIgnoreCase));

                if (matchedFile is null)
                {
                    var declaredOnDiff = finding.CitedSymbolFqns.Any(cite =>
                        payload.Files.Any(f => f.Methods.Any(m =>
                            m.Method == cite || cite.EndsWith("." + m.Method, StringComparison.Ordinal))));
                    if (!declaredOnDiff)
                    {
                        findingStatuses[finding] = "OffDiffLocation";
                        locationFailed = true;
                    }
                }
                else if (startLine > 0 && matchedFile.Methods.Count > 0 && matchedFile.Methods.Any(m => m.StartLine > 0))
                {
                    var end = endLine > 0 ? endLine : startLine;
                    var inRange = matchedFile.Methods.Any(m =>
                        (m.StartLine == 0 && m.EndLine == 0) ||
                        (startLine >= m.StartLine && (m.EndLine == 0 || end <= m.EndLine)));
                    if (!inRange)
                    {
                        findingStatuses[finding] = "OffDiffLocation";
                        locationFailed = true;
                    }
                }
            }

            // L2: Quote verification
            var currentFinding = finding;
            if (_options.EnableQuoteVerificationGuard && finding.OriginalCode is not null)
            {
                var (file, startLine, endLine) = ParseLocation(finding.Location);
                var actual = sourceResolver?.Invoke(file, startLine, endLine);
                if (actual is null || !IsQuoteMatch(actual, finding.OriginalCode))
                {
                    findingStatuses[finding] = "QuoteMismatch";
                    currentFinding = finding with { SuggestedCode = null };
                }
            }

            if (_options.Guard == GuardMode.Flag)
            {
                kept.Add(currentFinding);
                continue;
            }

            // Strip mode:
            if (locationFailed)
            {
                continue;
            }

            if (_options.RequireCitationsInStripMode && currentFinding.CitedSymbolFqns.Count == 0)
            {
                continue;
            }

            if (currentFinding.CitedSymbolFqns.Count > 0 && verified.Count == 0)
            {
                continue;
            }

            kept.Add(currentFinding with { CitedSymbolFqns = verified });
        }

        var findingAudit = findings.Select(f =>
        {
            var retained = kept.Any(k => k.Persona == f.Persona && k.Title == f.Title && k.Location == f.Location);
            if (findingStatuses.TryGetValue(f, out var specificStatus))
            {
                return new FindingAudit(f, retained, specificStatus);
            }
            var rejected = f.CitedSymbolFqns.Any(c => audit.Any(a => a.CitedFqn == c && !a.Verified));
            var status = f.CitedSymbolFqns.Count == 0 ? "UncitedAdvisory" : rejected ? "RejectedCitations" : "CitationExistenceOnly";
            return new FindingAudit(f, retained, status);
        }).ToList();

        return new GuardOutcome(kept, audit) { FindingAudit = findingAudit };
    }

    private static (string file, int startLine, int endLine) ParseLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return ("", 0, 0);
        var colon = location.LastIndexOf(':');
        if (colon < 0) return (location.Trim(), 0, 0);

        var file = location.Substring(0, colon).Trim();
        var lineSpan = location.Substring(colon + 1).Trim();
        var dash = lineSpan.IndexOf('-');
        if (dash > 0 && int.TryParse(lineSpan.Substring(0, dash), out var start) && int.TryParse(lineSpan.Substring(dash + 1), out var end))
            return (file, start, end);
        if (int.TryParse(lineSpan, out var single))
            return (file, single, single);
        return (file, 0, 0);
    }

            private static bool IsQuoteMatch(string actual, string expected) =>
        NormalizeCodeLines(actual) == NormalizeCodeLines(expected);

    private static string NormalizeCodeLines(string code)
    {
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }
}
