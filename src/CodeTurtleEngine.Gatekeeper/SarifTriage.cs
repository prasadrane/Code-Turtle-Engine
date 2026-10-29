using System.Text.Json;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record SarifDiagnostic(
    string RuleId,
    string Message,
    string FilePath,
    int StartLine,
    int EndLine,
    string Level = "warning");

public sealed record DiffHunk(
    string FilePath,
    int StartLine,
    int LineCount);

public static class SarifTriagePipeline
{
    public static IReadOnlyList<SarifDiagnostic> ParseSarifJson(string sarifJson)
    {
        var list = new List<SarifDiagnostic>();
        if (string.IsNullOrWhiteSpace(sarifJson)) return list;

        using var doc = JsonDocument.Parse(sarifJson);
        if (!doc.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var run in runs.EnumerateArray())
        {
            if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var result in results.EnumerateArray())
            {
                var ruleId = result.TryGetProperty("ruleId", out var rProp) ? rProp.GetString() ?? "" : "";
                var level = result.TryGetProperty("level", out var lProp) ? lProp.GetString() ?? "warning" : "warning";
                var message = "";
                if (result.TryGetProperty("message", out var mProp) && mProp.TryGetProperty("text", out var tProp))
                    message = tProp.GetString() ?? "";

                if (result.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var loc in locs.EnumerateArray())
                    {
                        if (loc.TryGetProperty("physicalLocation", out var pl))
                        {
                            var uri = "";
                            if (pl.TryGetProperty("artifactLocation", out var al) && al.TryGetProperty("uri", out var uProp))
                                uri = uProp.GetString() ?? "";

                            var startLine = 0;
                            var endLine = 0;
                            if (pl.TryGetProperty("region", out var reg))
                            {
                                if (reg.TryGetProperty("startLine", out var slProp)) startLine = slProp.GetInt32();
                                if (reg.TryGetProperty("endLine", out var elProp)) endLine = elProp.GetInt32();
                            }
                            if (endLine < startLine) endLine = startLine;

                            list.Add(new SarifDiagnostic(ruleId, message, NormalizePath(uri), startLine, endLine, level));
                        }
                    }
                }
            }
        }
        return list;
    }

    public static IReadOnlyList<SarifDiagnostic> IntersectWithDiffHunks(
        IEnumerable<SarifDiagnostic> diagnostics,
        IEnumerable<DiffHunk> hunks)
    {
        var hunkList = hunks.ToList();
        return diagnostics.Where(d =>
        {
            return hunkList.Any(h =>
                (NormalizePath(d.FilePath).EndsWith(NormalizePath(h.FilePath), StringComparison.OrdinalIgnoreCase) ||
                 NormalizePath(h.FilePath).EndsWith(NormalizePath(d.FilePath), StringComparison.OrdinalIgnoreCase)) &&
                (h.LineCount == 0 || (d.StartLine >= h.StartLine && d.StartLine < h.StartLine + Math.Max(1, h.LineCount))));
        }).ToList();
    }

    public static IReadOnlyList<SarifDiagnostic> Deduplicate(IEnumerable<SarifDiagnostic> diagnostics)
    {
        return diagnostics
            .GroupBy(d => $"{d.RuleId}|{NormalizePath(d.FilePath)}|{d.StartLine}|{d.Message.Trim()}")
            .Select(g => g.First())
            .ToList();
    }

    public static IReadOnlyList<ReviewFinding> ConvertToFindings(
        IEnumerable<SarifDiagnostic> diagnostics,
        PersonaRole persona = PersonaRole.SecurityAuditor)
    {
        return diagnostics.Select(d =>
        {
            var severity = d.Level.Equals("error", StringComparison.OrdinalIgnoreCase) || d.RuleId.StartsWith("CA3", StringComparison.OrdinalIgnoreCase)
                ? Severity.Error
                : Severity.Warning;
            var loc = d.StartLine > 0 ? $"{d.FilePath}:{d.StartLine}" : d.FilePath;
            return new ReviewFinding(persona, severity, d.RuleId, d.Message, loc, Array.Empty<string>());
        }).ToList();
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
