using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Integration.Tests;

public class LiveSmokeTests
{
    [LiveProviderFact]
    public async Task Bailian_Reachable_When_Live_Enabled()
    {
        // Use the product's trusted appsettings + environment bindings, including protocol.
        var services = Composition.BuildServices(AppContext.BaseDirectory);
        var options = services.GetRequiredService<LlmOptions>();
        ValidateConfiguration(options, Environment.GetEnvironmentVariable);
        var gw = services.GetRequiredService<ILlmGateway>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var reply = await gw.CompleteAsync("fast",
            new[] { new ChatMessage(ChatRole.User, "Reply with the single word: pong") },
            new ChatOptions { MaxOutputTokens = 16 }, deadline.Token);

        Assert.False(string.IsNullOrWhiteSpace(reply));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Live_Preflight_RejectsMissingCredentialsWithoutCallingProvider(string? value)
    {
        var options = ConfiguredOptions();
        var error = Assert.Throws<InvalidOperationException>(() =>
            ValidateConfiguration(options, name => name == "endpoint" ? "https://example.invalid" : value));
        Assert.Contains("credential", error.Message);
    }

    [Fact]
    public void Live_Preflight_RejectsMissingEndpointWithoutCallingProvider()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ValidateConfiguration(ConfiguredOptions(), name => name == "credential" ? "test-key" : null));
        Assert.Contains("endpoint", error.Message);
    }

    private static LlmOptions ConfiguredOptions() => new()
    {
        Routes = { new RouteOptions { BaseUrlEnv = "endpoint", ApiKeyEnv = "credential" } },
        ModelRoles = { ["fast"] = "configured-model" }
    };

    private static void ValidateConfiguration(LlmOptions options, Func<string, string?> readEnvironment)
    {
        if (options.Routes.Count == 0 || !options.ModelRoles.TryGetValue("fast", out var model)
            || string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Live validation requires a configured route and fast model.");
        foreach (var route in options.Routes)
        {
            foreach (var name in new[] { route.BaseUrlEnv, route.ApiKeyEnv })
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(readEnvironment(name)))
                    throw new InvalidOperationException($"Live validation blocked: configure environment variable '{name}'.");
        }
    }
}

public sealed class LiveProviderFactAttribute : FactAttribute
{
    public LiveProviderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TURTLE_LIVE") != "1")
            Skip = "Live provider validation requires explicit TURTLE_LIVE=1 and configured provider credentials.";
    }
}
