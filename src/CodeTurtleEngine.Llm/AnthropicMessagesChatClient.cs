using Microsoft.Extensions.AI;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CodeTurtleEngine.Llm;

/// <summary>
/// Speaks the Anthropic Messages protocol (POST {baseUrl}/v1/messages) so the gateway can
/// use relays that expose only that protocol (e.g. the Aliyun Token Plan endpoint). Non-streaming;
/// thinking blocks in the response are ignored, only type=="text" blocks form the reply.
/// Protocol shape confirmed by the 2026-09-09 curl spike (see docs/superpowers/specs/
/// 2026-09-09-anthropic-adapter-design.md).
/// </summary>
public sealed class AnthropicMessagesChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _maxTokens;

    public AnthropicMessagesChatClient(string baseUrl, string apiKey, string model,
        int maxTokens = 8192, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _maxTokens = maxTokens;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = new StringContent(BuildRequest(messages), Encoding.UTF8, "application/json")
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Anthropic Messages relay returned HTTP {(int)response.StatusCode}: {Truncate(body)}",
                inner: null,
                statusCode: response.StatusCode);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, ExtractText(body)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(IChatClient) ? this : null;

    public void Dispose() => _http.Dispose();

    private sealed record AnthropicMessage(string role, string content);

    private sealed record AnthropicRequest(
        string model, int max_tokens, List<AnthropicMessage> messages, string? system);

    private string BuildRequest(IEnumerable<ChatMessage> messages)
    {
        var system = new StringBuilder();
        var turns = new List<AnthropicMessage>();
        foreach (var m in messages)
        {
            if (m.Role == ChatRole.System)
            {
                if (system.Length > 0) system.Append('\n');
                system.Append(m.Text);
            }
            else
            {
                turns.Add(new AnthropicMessage(
                    m.Role == ChatRole.Assistant ? "assistant" : "user",
                    m.Text ?? ""));
            }
        }

        var payload = new AnthropicRequest(
            _model, _maxTokens, turns, system.Length > 0 ? system.ToString() : null);
        return JsonSerializer.Serialize(payload, CodeTurtleEngine.Core.TurtleJson.Options);
    }

    /// <summary>Concatenates only the type=="text" content blocks; thinking blocks are ignored.</summary>
    private static string ExtractText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array)
            throw new ModelException("Anthropic Messages response is missing a 'content' array.");

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(text.GetString());
            }
        }
        return sb.ToString();
    }

    private static string Truncate(string s, int max = 500)
        => s.Length <= max ? s : s[..max] + "...";
}
