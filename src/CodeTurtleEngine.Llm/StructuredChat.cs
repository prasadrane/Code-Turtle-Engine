using CodeTurtleEngine.Core;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeTurtleEngine.Llm;

public interface IStructuredChatClient
{
    Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default);
}

public sealed class StructuredChatClient : IStructuredChatClient
{
    private readonly ILlmGateway _gateway;
    private readonly int _jsonRetries;

    public StructuredChatClient(ILlmGateway gateway, int jsonRetries = 1)
    {
        _gateway = gateway;
        _jsonRetries = Math.Max(0, jsonRetries);
    }

    /// <summary>
    /// The JSON-schema instruction is ALWAYS part of the prompt: it is the only reliable structured-output
    /// path for the Anthropic-Messages protocol (no response_format), and the relay has no grammar guarantee,
    /// so client-side Parse+validate is mandatory. The OpenAI response_format attempt is kept as a bonus for
    /// "openai" routes; if it is rejected we degrade to the prompt-only call without it.
    /// When the model answers with malformed/invalid JSON (ModelException) the whole attempt is re-rolled up
    /// to jsonRetries times; transport errors (ProviderException) are NOT retried here — routing owns those.
    /// </summary>
    public async Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        using var schema = JsonDocument.Parse(jsonSchema);
        OutputSchema.Check(schema.RootElement);
        var instructed = WithSchemaInstruction(messages, jsonSchema);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await AttemptAsync<T>(role, instructed, jsonSchema, ct).ConfigureAwait(false);
            }
            catch (ModelException) when (attempt < _jsonRetries)
            {
                // Malformed JSON from the model: re-roll the gateway call below.
            }
        }
    }

    private async Task<T> AttemptAsync<T>(string role, IReadOnlyList<ChatMessage> instructed,
        string jsonSchema, CancellationToken ct)
    {
        try
        {
            var opts = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    JsonDocument.Parse(jsonSchema).RootElement, schemaName: "schema")
            };
            var raw = await _gateway.CompleteAsync(role, instructed, opts, ct).ConfigureAwait(false);
            return Parse<T>(raw, jsonSchema);
        }
        catch (Exception ex) when (IsUnsupportedFormat(ex))
        {
            var raw = await _gateway.CompleteAsync(role, instructed, null, ct).ConfigureAwait(false);
            return Parse<T>(raw, jsonSchema);
        }
    }

    private static IReadOnlyList<ChatMessage> WithSchemaInstruction(
        IReadOnlyList<ChatMessage> messages, string jsonSchema)
        => new List<ChatMessage>(messages)
        {
            new(ChatRole.User, $"Respond with ONLY a JSON value matching this schema, no prose:\n{jsonSchema}")
        };

    private static bool IsUnsupportedFormat(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is ProviderException provider)
            return provider.InnerException is not null && IsUnsupportedFormat(provider.InnerException);
        // Providers must explicitly identify the unsupported format capability.
        return (ex is NotSupportedException &&
                (ex.Message.Contains("response_format", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("schema", StringComparison.OrdinalIgnoreCase))) ||
            (ex is InvalidOperationException && ex.Message.Contains("schema unsupported", StringComparison.OrdinalIgnoreCase)) ||
            (ex is System.ClientModel.ClientResultException { Status: 400 } &&
                ex.Message.Contains("response_format", StringComparison.OrdinalIgnoreCase) &&
                ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    private static T Parse<T>(string raw, string jsonSchema)
    {
        var text = ExtractJson(raw);
        try
        {
            using var document = JsonDocument.Parse(text);
            using var schema = JsonDocument.Parse(jsonSchema);
            OutputSchema.Validate(document.RootElement, schema.RootElement);
            return JsonSerializer.Deserialize<T>(text, TurtleJson.Options)
                ?? throw new ModelException("Deserialized to null.");
        }
        catch (JsonException jx)
        {
            throw new ModelException($"Invalid JSON from model: {jx.Message}", jx);
        }
    }

    public static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('[')) return trimmed;
        var objectStart = raw.IndexOf('{');
        var arrayStart = raw.IndexOf('[');
        var start = objectStart < 0 ? arrayStart : arrayStart < 0 ? objectStart : Math.Min(objectStart, arrayStart);
        var end = start >= 0 && raw[start] == '[' ? raw.LastIndexOf(']') : raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw.Trim();
    }
}
