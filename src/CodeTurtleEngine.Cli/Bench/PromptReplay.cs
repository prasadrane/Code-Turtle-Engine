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

/// <summary>Serves independently recorded answers. Missing repetitions fail rather than recycling samples.</summary>
public sealed class ReplayChat : IStructuredChatClient
{
    private static readonly JsonSerializerOptions ReadOptions = new(CodeTurtleEngine.Core.TurtleJson.Options)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dir;
    private readonly Dictionary<string, int> _served = new(StringComparer.Ordinal);
    private int? _repetition;

    public ReplayChat(string dir) => _dir = dir;

    /// <summary>Pin the sample for both arms of an entry; identical prompts share that repetition's answer.</summary>
    public void SelectRepetition(int repetition)
    {
        if (repetition < 1) throw new ArgumentOutOfRangeException(nameof(repetition));
        lock (_served) _repetition = repetition;
    }

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var key = PromptExchange.Key(messages);
        int n;
        lock (_served) n = _repetition ?? (_served[key] = _served.GetValueOrDefault(key) + 1);
        var path = Path.Combine(_dir, key, $"{n}.json");
        if (!File.Exists(path))
            throw new ModelException($"No recorded answer for prompt {key}, repetition {n}.");

        VerdictDto? dto;
        try { dto = JsonSerializer.Deserialize<VerdictDto>(File.ReadAllText(path), ReadOptions); }
        catch (JsonException ex) { throw new ModelException($"Recorded answer {key}/{n}.json is not valid JSON: {ex.Message}", ex); }
        if (dto?.Findings is null)
            throw new ModelException($"Recorded answer {key}/{n}.json has no Findings array.");
        return Task.FromResult((T)(object)dto);
    }
}
