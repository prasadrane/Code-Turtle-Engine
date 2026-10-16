using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Cli.Bench;

/// <summary>
/// Offline LLM exchange for the benchmark: prompts are exported once, answered out of band
/// (any model, any harness), then replayed through the real pipeline, guard and scorer.
/// Layout: &lt;dir&gt;/&lt;key&gt;/prompt.md and &lt;dir&gt;/&lt;key&gt;/&lt;n&gt;.json (n = 1-based repetition).
/// </summary>
public static class PromptExchange
{
    /// <summary>Stable id of one persona prompt: SHA-256 of role-tagged message text, first 16 hex chars.</summary>
    public static string Key(IReadOnlyList<ChatMessage> messages)
    {
        var text = string.Join('\u0000', messages.Select(m => $"{m.Role.Value}\n{m.Text}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
    }
}

/// <summary>Writes every prompt it is asked to complete and returns an empty verdict so the run can proceed.</summary>
public sealed class RecordingChat : IStructuredChatClient
{
    private readonly string _dir;
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public RecordingChat(string dir) => _dir = dir;

    public int PromptCount
    {
        get { lock (_keys) return _keys.Count; }
    }

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var key = PromptExchange.Key(messages);
        bool isNew;
        lock (_keys) isNew = _keys.Add(key);
        if (isNew)
        {
            var dir = Path.Combine(_dir, key);
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            foreach (var m in messages)
                sb.Append("=== ").Append(m.Role.Value.ToUpperInvariant()).Append(" ===\n").Append(m.Text).Append("\n\n");
            sb.Append("=== OUTPUT SCHEMA (JSON Schema) ===\n").Append(jsonSchema).Append('\n');
            File.WriteAllText(Path.Combine(dir, "prompt.md"), sb.ToString());
        }
        return Task.FromResult((T)(object)new VerdictDto(Array.Empty<FindingDto>()));
    }
}

public sealed record AnswerProvenance(int Repetition, string AnswerSha256);

public sealed record PromptProvenance(string Key, string? PromptSha256, IReadOnlyList<AnswerProvenance>? Answers);

public sealed record ReplayProvenance(string Model, string Harness, string Provenance, IReadOnlyList<PromptProvenance>? Prompts = null);

/// <summary>Serves independently recorded answers. Missing repetitions fail rather than recycling samples.</summary>
public sealed class ReplayChat : IStructuredChatClient
{
    private static readonly JsonSerializerOptions ReadOptions = new(CodeTurtleEngine.Core.TurtleJson.Options)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dir;
    private readonly ReplayProvenance? _provenance;
    private readonly Dictionary<(string Key, int Repetition), string> _answerHashes = new();
    private readonly Dictionary<string, string> _promptHashes = new(StringComparer.Ordinal);
    private int _repetition;

    public ReplayChat(string dir)
    {
        _dir = dir;
        var provPath = Path.Combine(dir, "provenance.json");
        if (!File.Exists(provPath)) return;
        _provenance = JsonSerializer.Deserialize<ReplayProvenance>(File.ReadAllText(provPath), ReadOptions)
            ?? throw new BenchException($"Provenance manifest '{provPath}' is empty.");
        foreach (var p in _provenance.Prompts ?? Array.Empty<PromptProvenance>())
        {
            if (!string.IsNullOrWhiteSpace(p.PromptSha256)) _promptHashes[p.Key] = p.PromptSha256;
            foreach (var a in p.Answers ?? Array.Empty<AnswerProvenance>())
                _answerHashes[(p.Key, a.Repetition)] = a.AnswerSha256;
        }
    }

    public string DescribeRoute()
    {
        var name = Path.GetFileName(Path.GetFullPath(_dir));
        return _provenance is null
            ? $"replay:{name}"
            : $"replay:{name} ({_provenance.Model} via {_provenance.Harness}, {_provenance.Provenance})";
    }

    /// <summary>Pin the sample for both arms of an entry; identical prompts share that repetition's answer.</summary>
    public void SelectRepetition(int repetition)
    {
        if (repetition < 1) throw new ArgumentOutOfRangeException(nameof(repetition));
        Volatile.Write(ref _repetition, repetition);
    }

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var n = Volatile.Read(ref _repetition);
        if (n < 1)
            throw new ModelException("SelectRepetition must be called before replaying prompts.");
        var key = PromptExchange.Key(messages);
        var path = Path.Combine(_dir, key, $"{n}.json");
        if (!File.Exists(path))
            throw new ModelException($"No recorded answer for prompt {key}, repetition {n}.");

        var bytes = File.ReadAllBytes(path);
        if (_answerHashes.TryGetValue((key, n), out var expectedSha))
        {
            var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                throw new ModelException($"Recorded answer {key}/{n}.json SHA-256 mismatch (expected {expectedSha}, got {actualSha}).");
        }
        var promptPath = Path.Combine(_dir, key, "prompt.md");
        if (_promptHashes.TryGetValue(key, out var expectedPromptSha) && File.Exists(promptPath))
        {
            var actualPromptSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(promptPath))).ToLowerInvariant();
            if (!string.Equals(actualPromptSha, expectedPromptSha, StringComparison.OrdinalIgnoreCase))
                throw new ModelException($"Recorded prompt {key}/prompt.md SHA-256 mismatch.");
        }

        VerdictDto? dto;
        try { dto = JsonSerializer.Deserialize<VerdictDto>(bytes, ReadOptions); }
        catch (JsonException ex) { throw new ModelException($"Recorded answer {key}/{n}.json is not valid JSON: {ex.Message}", ex); }
        if (dto?.Findings is null)
            throw new ModelException($"Recorded answer {key}/{n}.json has no Findings array.");
        return Task.FromResult((T)(object)dto);
    }
}
