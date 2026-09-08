using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

internal sealed class FakeFactory : IChatClientFactory
{
    private readonly Func<RouteOptions, string, IChatClient> _make;
    public FakeFactory(Func<RouteOptions, string, IChatClient> make) => _make = make;
    public IChatClient Create(RouteOptions route, string model) => _make(route, model);
}

internal sealed class FakeGateway : ILlmGateway
{
    private readonly Func<string, IReadOnlyList<ChatMessage>, ChatOptions?, Task<string>> _fn;
    public FakeGateway(Func<string, IReadOnlyList<ChatMessage>, ChatOptions?, Task<string>> fn) => _fn = fn;
    public Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default) => _fn(role, messages, options);
}
