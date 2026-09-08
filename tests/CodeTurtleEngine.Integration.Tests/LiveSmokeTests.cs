using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Integration.Tests;

public class LiveSmokeTests
{
    [Fact]
    public async Task Bailian_Reachable_When_Live_Enabled()
    {
        if (Environment.GetEnvironmentVariable("TURTLE_LIVE") != "1") return; // skipped by default

        var options = new LlmOptions
        {
            Routes = { new RouteOptions { Name = "bailian", BaseUrlEnv = "TURTLE_LLM_BASE_URL", ApiKeyEnv = "TURTLE_LLM_API_KEY", Model = "qwen3.8-flash" } },
            ModelRoles = { ["fast"] = "qwen3.8-flash" }
        };
        var gw = new LlmGateway(options, new BailianChatClientFactory());
        var reply = await gw.CompleteAsync("fast", new[] { new ChatMessage(ChatRole.User, "Reply with the single word: pong") });

        Assert.False(string.IsNullOrWhiteSpace(reply));
    }
}
