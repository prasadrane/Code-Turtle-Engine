using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

public class ChatClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", null);
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", null);
    }

    private static RouteOptions Route() => new()
    {
        Name = "r",
        BaseUrlEnv = "TURTLE_TEST_BASE",
        ApiKeyEnv = "TURTLE_TEST_KEY",
        Model = "qwen3.8-flash"
    };

    [Fact]
    public void Throws_ProviderException_When_BaseUrl_Env_Missing()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        var ex = Assert.Throws<ProviderException>(() => new BailianChatClientFactory().Create(Route(), "qwen3.8-flash"));
        Assert.Contains("TURTLE_TEST_BASE", ex.Message);
    }

    [Fact]
    public void Builds_Client_When_Env_Set()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", "https://localhost/v1");
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        IChatClient client = new BailianChatClientFactory().Create(Route(), "qwen3.8-flash");
        Assert.NotNull(client);
    }

    [Fact]
    public void Anthropic_Protocol_Builds_Anthropic_Client()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", "https://relay.test/apps/anthropic");
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        var route = Route();
        route.Protocol = "anthropic";
        IChatClient client = new BailianChatClientFactory().Create(route, "qwen3.8-flash");
        Assert.IsType<AnthropicMessagesChatClient>(client);
    }

    [Fact]
    public void Default_Protocol_Builds_OpenAI_Client()
    {
        Environment.SetEnvironmentVariable("TURTLE_TEST_BASE", "https://localhost/v1");
        Environment.SetEnvironmentVariable("TURTLE_TEST_KEY", "k");
        IChatClient client = new BailianChatClientFactory().Create(Route(), "qwen3.8-flash");
        Assert.IsNotType<AnthropicMessagesChatClient>(client);
    }

    [Fact]
    public async Task MockChatClient_Returns_Canned_And_Counts()
    {
        var mock = new MockChatClient("{\"ok\":true}");
        var resp = await mock.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });
        Assert.Equal("{\"ok\":true}", resp.Text);
        Assert.Equal(1, mock.CallCount);
    }
}
