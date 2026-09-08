using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CodeTurtleEngine.Llm;

public sealed class MockChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, string> _responder;

    public int CallCount { get; private set; }

    public MockChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, string> responder)
        => _responder = responder;

    public MockChatClient(string fixedJson) : this((_, _) => fixedJson) { }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        var text = _responder(messages, options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(IChatClient) ? this : null;

    public void Dispose() { }
}
