using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;
using Polly;

namespace CodeTurtleEngine.Llm.Tests;

public class LlmGatewayTests
{
    private static LlmOptions Opts() => new()
    {
        Routes =
        {
            new RouteOptions { Name = "primary", Model = "qwen3.8-flash" },
            new RouteOptions { Name = "backup", Model = "qwen3.8-flash" }
        },
        ModelRoles = { ["fast"] = "qwen3.8-flash" }
    };

    private static IReadOnlyList<ChatMessage> Msgs() => new[] { new ChatMessage(ChatRole.User, "hi") };

    [Fact]
    public async Task Falls_Back_To_Second_Route_When_First_Fails()
    {
        var factory = new FakeFactory((route, _) => route.Name == "primary"
            ? new MockChatClient((_, _) => throw new InvalidOperationException("primary down"))
            : new MockChatClient("{\"ok\":true}"));

        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.Empty);
        var result = await gw.CompleteAsync("fast", Msgs());

        Assert.Equal("{\"ok\":true}", result);
    }

    [Fact]
    public async Task Throws_ProviderException_When_All_Routes_Fail()
    {
        var factory = new FakeFactory((_, _) => new MockChatClient((_, _) => throw new InvalidOperationException("down")));
        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.Empty);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => gw.CompleteAsync("fast", Msgs()));
        Assert.Equal(2, ex.AttemptLog.Count);
    }

    [Fact]
    public async Task Propagates_OperationCanceledException_When_Ct_Cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var factory = new FakeFactory((_, _) =>
            new MockChatClient((_, _) => throw new OperationCanceledException()));

        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.Empty);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => gw.CompleteAsync("fast", Msgs(), ct: cts.Token));
    }

    [Fact]
    public async Task Throws_ModelException_For_Unknown_Role()
    {
        var factory = new FakeFactory((_, _) => new MockChatClient("x"));
        var gw = new LlmGateway(Opts(), factory, ResiliencePipeline.Empty);

        await Assert.ThrowsAsync<ModelException>(() => gw.CompleteAsync("nope", Msgs()));
    }
}
