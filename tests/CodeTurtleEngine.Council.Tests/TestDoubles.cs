using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Council.Tests;

internal sealed class FakeStructuredChat : IStructuredChatClient
{
    private readonly Func<string, IReadOnlyList<ChatMessage>, object> _responder;

    public List<string> RolesCalled { get; } = new();

    public FakeStructuredChat(Func<string, IReadOnlyList<ChatMessage>, object> responder)
        => _responder = responder;

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        lock (RolesCalled) RolesCalled.Add(role);
        return Task.FromResult((T)_responder(role, messages));
    }
}
