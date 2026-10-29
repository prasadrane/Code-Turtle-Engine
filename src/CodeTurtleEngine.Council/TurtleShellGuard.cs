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
            if (_options.EnableHunkIntersectionGuard)
            {
                var (file, startLine, endLine, valid) = ParseLocation(finding.Location);
                if (!valid)
                {
                    findingStatuses[finding] = "MalformedLocation";
                    locationFailed = true;
                }
                else if (payload.Files.Count > 0)
                {
                    var matchedFile = payload.Files.FirstOrDefault(f => PathMatches(f.FilePath, file));
                    if (matchedFile is null)
                    {
                        findingStatuses[finding] = "OffDiffLocation";
                        locationFailed = true;
                    }
                    else if (matchedFile.DiffHunks is { Count: > 0 } hunks)
                    {
                        var inHunk = hunks.Any(h => h.LineCount > 0 &&
                            startLine >= h.StartLine &&
                            endLine <= (h.StartLine + h.LineCount - 1));
                        if (!inHunk)
                        {
                            findingStatuses[finding] = "OffDiffLocation";
                            locationFailed = true;
                        }
                    }
                    else if (matchedFile.Methods.Count > 0 && matchedFile.Methods.Any(m => m.StartLine > 0))
                    {
                        var inRange = matchedFile.Methods.Any(m =>
                            (m.StartLine == 0 && m.EndLine == 0) ||
                            (startLine >= m.StartLine && (m.EndLine == 0 || endLine <= m.EndLine)));
                        if (!inRange)
                        {
                            findingStatuses[finding] = "OffDiffLocation";
                            locationFailed = true;
                        }
                    }
                }
            }

            // L2: Quote verification
            var currentFinding = finding;
            if (_options.EnableQuoteVerificationGuard)
            {
                if (finding.SuggestedCode is not null)
                {
                    if (string.IsNullOrWhiteSpace(finding.OriginalCode))
                    {
                        findingStatuses[finding] = "MissingQuote";
                        currentFinding = finding with { SuggestedCode = null };
                    }
                    else
                    {
                        var (file, startLine, endLine, valid) = ParseLocation(finding.Location);
                        var actual = valid ? sourceResolver?.Invoke(file, startLine, endLine) : null;
                        if (actual is null || !IsQuoteMatch(actual, finding.OriginalCode))
                        {
                            findingStatuses[finding] = "QuoteMismatch";
                            currentFinding = finding with { SuggestedCode = null };
                        }
                    }
                }
                else if (finding.OriginalCode is not null && sourceResolver is not null)
                {
                    var (file, startLine, endLine, valid) = ParseLocation(finding.Location);
                    var actual = valid ? sourceResolver.Invoke(file, startLine, endLine) : null;
                    if (actual is null || !IsQuoteMatch(actual, finding.OriginalCode))
                    {
                        findingStatuses[finding] = "QuoteMismatch";
                    }
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

    private static (string file, int startLine, int endLine, bool valid) ParseLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return ("", 0, 0, false);
        var colon = location.LastIndexOf(':');
        if (colon <= 0 || colon >= location.Length - 1) return ("", 0, 0, false);

        var file = location.Substring(0, colon).Trim();
        if (string.IsNullOrWhiteSpace(file)) return ("", 0, 0, false);

        var lineSpan = location.Substring(colon + 1).Trim();
        var dash = lineSpan.IndexOf('-');
        if (dash >= 0)
        {
            if (dash == 0 || dash == lineSpan.Length - 1) return (file, 0, 0, false);
            var part1 = lineSpan.Substring(0, dash);
            var part2 = lineSpan.Substring(dash + 1);
            if (part2.Contains('-')) return (file, 0, 0, false);
            if (int.TryParse(part1, out var start) && int.TryParse(part2, out var end))
            {
                if (start > 0 && end > 0 && end >= start)
                    return (file, start, end, true);
            }
            return (file, 0, 0, false);
        }

        if (int.TryParse(lineSpan, out var single) && single > 0)
            return (file, single, single, true);

        return (file, 0, 0, false);
    }

    private static bool PathMatches(string pathA, string pathB)
    {
        var normA = pathA.Replace('\\', '/').TrimStart('/');
        var normB = pathB.Replace('\\', '/').TrimStart('/');
        return normA.Equals(normB, StringComparison.OrdinalIgnoreCase) ||
               normA.EndsWith("/" + normB, StringComparison.OrdinalIgnoreCase) ||
               normB.EndsWith("/" + normA, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuoteMatch(string actual, string expected) =>
        NormalizeSnippet(actual) == NormalizeSnippet(expected);

    private static string NormalizeSnippet(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        int start = 0;
        while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
        int end = lines.Length - 1;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) end--;

        if (start > end) return string.Empty;

        int minIndent = int.MaxValue;
        for (int i = start; i <= end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            int indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;
            if (indent < minIndent) minIndent = indent;
        }
        if (minIndent == int.MaxValue) minIndent = 0;

        var resultLines = new List<string>(end - start + 1);
        for (int i = start; i <= end; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                resultLines.Add(string.Empty);
            }
            else
            {
                var stripped = line.Length >= minIndent ? line.Substring(minIndent) : line.TrimStart();
                resultLines.Add(stripped);
            }
        }

        return string.Join("\n", resultLines);
    }
}
