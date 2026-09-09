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

    public StructuredChatClient(ILlmGateway gateway) => _gateway = gateway;

    /// <summary>
    /// The JSON-schema instruction is ALWAYS part of the prompt: it is the only reliable structured-output
    /// path for the Anthropic-Messages protocol (no response_format), and the relay has no grammar guarantee,
    /// so client-side Parse+validate is mandatory. The OpenAI response_format attempt is kept as a bonus for
    /// "openai" routes; if it is rejected we degrade to the prompt-only call without it.
    /// </summary>
    public async Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        var instructed = WithSchemaInstruction(messages, jsonSchema);
        try
        {
            var opts = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    JsonDocument.Parse(jsonSchema).RootElement, schemaName: "schema")
            };
            var raw = await _gateway.CompleteAsync(role, instructed, opts, ct);
            return Parse<T>(raw);
        }
        catch (Exception ex) when (ex is not ModelException)
        {
            var raw = await _gateway.CompleteAsync(role, instructed, null, ct);
            return Parse<T>(raw);
        }
    }

    private static IReadOnlyList<ChatMessage> WithSchemaInstruction(
        IReadOnlyList<ChatMessage> messages, string jsonSchema)
        => new List<ChatMessage>(messages)
        {
            new(ChatRole.User, $"Respond with ONLY a JSON value matching this schema, no prose:\n{jsonSchema}")
        };

    private static T Parse<T>(string raw)
    {
        var text = ExtractJson(raw);
        try
        {
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
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw.Trim();
    }
}
