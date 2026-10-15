using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Cli.Bench;

public enum CorpusKind { Seeded, Oss }

public sealed record ExpectedDefect(string File, int Line, string Class, string? Method = null);

public sealed record CorpusEntry(
    string Id,
    CorpusKind Kind,
    string Project,
    string? BaseDir = null,
    string? HeadDir = null,
    string? CloneUrl = null,
    string? BaseSha = null,
    string? HeadSha = null,
    IReadOnlyList<ExpectedDefect>? Expected = null);

public sealed record CorpusManifest(
    IReadOnlyList<CorpusEntry> Entries,
    IReadOnlyDictionary<string, PersonaRole> ClassPersonas);

public sealed class BenchException(string message) : Exception(message);

public static class BenchJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public static partial class CorpusLoader
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")] private static partial Regex IdPattern();
    [GeneratedRegex("^[0-9a-fA-F]{40}$")] private static partial Regex ShaPattern();

    public static CorpusManifest Load(string manifestPath)
    {
        CorpusManifest? raw;
        try { raw = JsonSerializer.Deserialize<CorpusManifest>(File.ReadAllText(manifestPath), BenchJson.Options); }
        catch (JsonException ex) { throw new BenchException($"Corpus manifest is not valid JSON: {ex.Message}"); }
        if (raw?.Entries is null || raw.Entries.Count == 0)
            throw new BenchException("Corpus manifest must contain at least one entry.");

        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var classes = raw.ClassPersonas ?? new Dictionary<string, PersonaRole>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<CorpusEntry>();
        foreach (var e in raw.Entries)
        {
            if (!IdPattern().IsMatch(e.Id ?? "")) throw new BenchException($"{e.Id}: id must match ^[a-z0-9][a-z0-9-]*$.");
            if (!seen.Add(e.Id!)) throw new BenchException($"{e.Id}: duplicate id.");
            var resolved = e.Kind == CorpusKind.Seeded ? ValidateSeeded(e, root) : ValidateOss(e);
            foreach (var d in resolved.Expected ?? Array.Empty<ExpectedDefect>())
            {
                if (d.Line < 1) throw new BenchException($"{e.Id}: expected line must be >= 1 ({d.File}).");
                if (!classes.ContainsKey(d.Class)) throw new BenchException($"{e.Id}: defect class '{d.Class}' missing from classPersonas.");
            }
            entries.Add(resolved);
        }
        return new CorpusManifest(entries, classes);
    }

    private static CorpusEntry ValidateSeeded(CorpusEntry e, string root)
    {
        var baseDir = Path.GetFullPath(Path.Combine(root, e.BaseDir ?? ""));
        var headDir = Path.GetFullPath(Path.Combine(root, e.HeadDir ?? ""));
        if (e.BaseDir is null || !Directory.Exists(baseDir)) throw new BenchException($"{e.Id}: baseDir '{e.BaseDir}' not found.");
        if (e.HeadDir is null || !Directory.Exists(headDir)) throw new BenchException($"{e.Id}: headDir '{e.HeadDir}' not found.");
        if (!File.Exists(Path.Combine(baseDir, e.Project))) throw new BenchException($"{e.Id}: project '{e.Project}' not found under baseDir.");
        if (e.Expected is not null) throw new BenchException($"{e.Id}: seeded entries derive expected defects from // BENCH: markers; remove 'expected'.");
        return e with { BaseDir = baseDir, HeadDir = headDir };
    }

    private static CorpusEntry ValidateOss(CorpusEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.CloneUrl)) throw new BenchException($"{e.Id}: cloneUrl is required.");
        if (!ShaPattern().IsMatch(e.BaseSha ?? "")) throw new BenchException($"{e.Id}: baseSha must be a 40-character commit SHA.");
        if (!ShaPattern().IsMatch(e.HeadSha ?? "")) throw new BenchException($"{e.Id}: headSha must be a 40-character commit SHA.");
        return e;
    }
}
