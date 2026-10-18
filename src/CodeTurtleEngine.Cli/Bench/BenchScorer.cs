using System.Globalization;
using System.Text.RegularExpressions;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli.Bench;

public sealed record RunScore(
    int Findings,
    int Citations,
    int Unverified,
    int Invented,
    int FindingsWithInvented,
    int? Matched,
    int? ExpectedCount)
{
    public int? CitedFindings { get; init; }
    public int? UncitedFindings => CitedFindings is null ? null : Findings - CitedFindings.Value;
    public int RejectedCitations => Invented; // Same existence oracle as the current guard policy.
    public double? CitationCoverage => Findings == 0 || CitedFindings is null ? null : (double)CitedFindings.Value / Findings;
    public double? UnverifiedRate => Citations == 0 ? null : (double)Unverified / Citations;
    public double? InventedRate => Citations == 0 ? null : (double)Invented / Citations;
    public double? Precision => Matched is null || Findings == 0 ? null : (double)Matched.Value / Findings;
    public double? Recall => Matched is null || ExpectedCount is null or 0 ? null : (double)Matched.Value / ExpectedCount.Value;
}

/// <summary>Deterministic scoring: no LLM, no I/O.</summary>
public static partial class BenchScorer
{
    public const int LineTolerance = 3;

    [GeneratedRegex(@"^(?<file>.+?\.cs):(?<line>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LocationPattern();

    public static RunScore Score(
        IReadOnlyList<ReviewFinding> findings,
        IReadOnlySet<string> allowList,
        Func<string, bool> exists,
        IReadOnlyList<ExpectedDefect>? expected,
        IReadOnlyDictionary<string, PersonaRole> classPersonas)
    {
        int citations = 0, unverified = 0, invented = 0, findingsWithInvented = 0;
        foreach (var f in findings)
        {
            var anyInvented = false;
            foreach (var cite in f.CitedSymbolFqns)
            {
                citations++;
                if (!allowList.Contains(cite)) unverified++;
                if (!exists(cite)) { invented++; anyInvented = true; }
            }
            if (anyInvented) findingsWithInvented++;
        }

        int? matched = expected is null ? null : Match(findings, expected, classPersonas);
        return new RunScore(findings.Count, citations, unverified, invented, findingsWithInvented, matched, expected?.Count)
            { CitedFindings = findings.Count(f => f.CitedSymbolFqns.Count > 0) };
    }

    public static bool TryParseLocation(string location, out string file, out int line)
    {
        file = "";
        line = 0;
        var m = LocationPattern().Match(location.Trim());
        if (!m.Success) return false;
        file = m.Groups["file"].Value;
        return int.TryParse(m.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out line);
    }

    [GeneratedRegex(@"(?<![\w./\\-])(?<file>[\w./\\-]*[\w-]\.cs)(?![\w])", RegexOptions.IgnoreCase)]
    private static partial Regex FileTokenPattern();

    private static bool ContainsIdentifier(string text, string identifier)
    {
        var start = 0;
        while (start <= text.Length - identifier.Length)
        {
            var i = text.IndexOf(identifier, start, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            var before = i == 0 || !(char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '_');
            var end = i + identifier.Length;
            var after = end >= text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_');
            if (before && after) return true;
            start = i + 1;
        }
        return false;
    }

    private static int Match(IReadOnlyList<ReviewFinding> findings, IReadOnlyList<ExpectedDefect> expected,
        IReadOnlyDictionary<string, PersonaRole> classPersonas)
    {
        var ordered = findings.Select((f, i) => (f, i))
            .OrderByDescending(x => x.f.Severity).ThenBy(x => x.i).ToList();
        var used = new HashSet<int>();
        var matched = 0;
        foreach (var d in expected)
        {
            var persona = classPersonas[d.Class];
            foreach (var (f, i) in ordered)
            {
                if (used.Contains(i) || f.Persona != persona) continue;
                var hasLine = TryParseLocation(f.Location, out var file, out var line);
                if (!hasLine)
                {
                    var tok = FileTokenPattern().Match(f.Location);
                    if (!tok.Success) continue;
                    file = tok.Groups["file"].Value;
                }
                if (!SameFile(file, d.File)) continue;
                var byLine = hasLine && Math.Abs(line - d.Line) <= LineTolerance;
                var byMethod = d.Method is not null
                    && (ContainsIdentifier(f.Location, d.Method) || ContainsIdentifier(f.Title, d.Method));
                if (!byLine && !byMethod) continue;
                used.Add(i);
                matched++;
                break;
            }
        }
        return matched;
    }

    private static bool SameFile(string a, string b) => RepoPaths.EndsWith(a, b) || RepoPaths.EndsWith(b, a);
}
