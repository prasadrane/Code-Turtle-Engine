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

    public async Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        try
        {
            var opts = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    JsonDocument.Parse(jsonSchema).RootElement, schemaName: "schema")
            };
            var raw = await _gateway.CompleteAsync(role, messages, opts, ct);
            return Parse<T>(raw);
        }
        catch (Exception ex) when (ex is not ModelException)
        {
            var degrade = new List<ChatMessage>(messages)
            {
                new(ChatRole.User, $"Respond with ONLY a JSON value matching this schema, no prose:\n{jsonSchema}")
            };
            var raw = await _gateway.CompleteAsync(role, degrade, null, ct);
            return Parse<T>(raw);
        }
    }

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
