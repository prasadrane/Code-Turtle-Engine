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
    public double UnverifiedRate => Citations == 0 ? 0 : (double)Unverified / Citations;
    public double InventedRate => Citations == 0 ? 0 : (double)Invented / Citations;
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
        return new RunScore(findings.Count, citations, unverified, invented, findingsWithInvented, matched, expected?.Count);
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
                if (!TryParseLocation(f.Location, out var file, out var line)) continue;
                if (!SameFile(file, d.File) || Math.Abs(line - d.Line) > LineTolerance) continue;
                used.Add(i);
                matched++;
                break;
            }
        }
        return matched;
    }

    private static bool SameFile(string a, string b) => RepoPaths.EndsWith(a, b) || RepoPaths.EndsWith(b, a);
}
