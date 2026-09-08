using CodeTurtleEngine.Core;
using System.Text;

namespace CodeTurtleEngine.Council;

public interface IArbiter
{
    IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts);
    string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload);
}

public sealed class Arbiter : IArbiter
{
    public IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts) =>
        verdicts.SelectMany(v => v.Findings)
            .GroupBy(f => (Normalize(f.Title), f.Location))
            .Select(g => g.OrderByDescending(f => (int)f.Severity).First())
            .OrderByDescending(f => (int)f.Severity)
            .ThenBy(f => f.Location, StringComparer.Ordinal)
            .ToList();

    public string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Code Turtle Review - {payload.RepoSlug}");
        sb.AppendLine();
        sb.AppendLine($"Diff baseline: `{payload.DiffBaseline}` - Files reviewed: {payload.Files.Count}");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("_No issues found._");
            return sb.ToString();
        }

        foreach (var f in findings)
        {
            sb.AppendLine($"## [{f.Severity}] {f.Title}");
            sb.AppendLine($"- **Persona:** {f.Persona}");
            sb.AppendLine($"- **Location:** `{f.Location}`");
            sb.AppendLine($"- {f.Detail}");
            if (f.CitedSymbolFqns.Count > 0)
                sb.AppendLine($"- **Symbols:** {string.Join(", ", f.CitedSymbolFqns.Select(s => $"`{s}`"))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Normalize(string title) => title.Trim().ToLowerInvariant();
}
