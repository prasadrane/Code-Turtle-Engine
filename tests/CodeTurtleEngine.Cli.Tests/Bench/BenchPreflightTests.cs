using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class BenchPreflightTests
{
    private static LlmOptions Llm() => new()
    {
        Routes = { new RouteOptions { Name = "r", Model = "m", BaseUrlEnv = "X_URL", ApiKeyEnv = "X_KEY" } }
    };

    [Fact]
    public void Missing_Credentials_Are_Named_Without_Values()
    {
        var ex = Assert.Throws<BenchException>(() =>
            BenchPreflight.RequireCredentials(Llm(), n => n == "X_URL" ? "https://secret.example" : null));
        Assert.Contains("X_KEY", ex.Message);
        Assert.DoesNotContain("X_URL", ex.Message);
        Assert.DoesNotContain("secret", ex.Message);
    }

    [Fact]
    public void Present_Credentials_Pass() =>
        BenchPreflight.RequireCredentials(Llm(), _ => "set");

    [Fact]
    public void No_Routes_Fails() =>
        Assert.Throws<BenchException>(() => BenchPreflight.RequireCredentials(new LlmOptions(), _ => "set"));
}
