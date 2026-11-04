using CodeTurtleEngine.Core;
using System.Text;

namespace CodeTurtleEngine.Council;

public interface IArbiter
{
    IReadOnlyList<ReviewFinding> Merge(IReadOnlyList<PersonaVerdict> verdicts);
    string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, PromptCoverage? coverage = null);
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

    public string RenderMarkdown(IReadOnlyList<ReviewFinding> findings, RoslynPayload payload, PromptCoverage? coverage = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Code Turtle Review - {payload.RepoSlug}");
        sb.AppendLine();
        sb.AppendLine($"Diff baseline: `{payload.DiffBaseline}`");
        if (coverage is null)
            sb.AppendLine($"Extracted file contexts: {payload.Files.Count}. Model prompt coverage unavailable.");
        else
        {
            sb.AppendLine($"File contexts: {coverage.ExtractedFiles} extracted, {coverage.SubmittedFiles} submitted to model prompts, {coverage.OmittedFiles} omitted.");
            sb.AppendLine($"Member contexts: {coverage.ExtractedMethods} extracted, {coverage.SubmittedMethods} submitted to model prompts, {coverage.OmittedMethods} omitted.");
            if (coverage.OmittedFiles > 0 || coverage.OmittedMethods > 0)
                sb.AppendLine("> Partial model input: prompt limits omitted extracted contexts. Citation checks still use the full payload.");
        }
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("_No retained findings. This does not establish that the code is correct or clean._");
            return sb.ToString();
        }

        sb.AppendLine("_Citation checks establish existence only; recommendations, relevance and locations are not verified._");
        sb.AppendLine();
        foreach (var f in findings)
        {
            sb.AppendLine($"## [{f.Severity}] {f.Title}");
            sb.AppendLine($"- **Persona:** {f.Persona}");
            sb.AppendLine($"- **Location:** `{f.Location}`");
            sb.AppendLine($"- {f.Detail}");
            if (f.CitedSymbolFqns.Count > 0)
                sb.AppendLine($"- **Symbols:** {string.Join(", ", f.CitedSymbolFqns.Select(s => $"`{s}`"))}");
            else
                sb.AppendLine("- **Citation status:** Uncited advisory finding (no existence verification).");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Normalize(string title) => title.Trim().ToLowerInvariant();
}
