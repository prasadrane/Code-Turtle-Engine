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

public static class SarifTriagePipeline
{
    public static IReadOnlyList<SarifDiagnostic> ParseSarifJson(string sarifJson)
    {
        var list = new List<SarifDiagnostic>();
        if (string.IsNullOrWhiteSpace(sarifJson)) return list;

        using var doc = JsonDocument.Parse(sarifJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var run in runs.EnumerateArray())
        {
            if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var result in results.EnumerateArray())
            {
                var ruleId = result.TryGetProperty("ruleId", out var r) ? r.GetString() ?? "UNKNOWN" : "UNKNOWN";
                var message = result.TryGetProperty("message", out var m) && m.TryGetProperty("text", out var t)
                    ? t.GetString() ?? "" : "";
                var level = result.TryGetProperty("level", out var l) ? l.GetString() ?? "warning" : "warning";

                if (result.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var loc in locs.EnumerateArray())
                    {
                        if (loc.TryGetProperty("physicalLocation", out var pl))
                        {
                            var uri = pl.TryGetProperty("artifactLocation", out var al) && al.TryGetProperty("uri", out var u)
                                ? u.GetString() ?? "" : "";
                            var region = pl.TryGetProperty("region", out var reg) ? reg : default;
                            var startLine = region.ValueKind == JsonValueKind.Object && region.TryGetProperty("startLine", out var sl)
                                ? sl.GetInt32() : 0;
                            var endLine = region.ValueKind == JsonValueKind.Object && region.TryGetProperty("endLine", out var el)
                                ? el.GetInt32() : startLine;

                            if (startLine <= 0) continue;
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
                h.LineCount > 0 &&
                d.StartLine >= h.StartLine &&
                d.StartLine < h.StartLine + h.LineCount &&
                d.EndLine < h.StartLine + h.LineCount);
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
