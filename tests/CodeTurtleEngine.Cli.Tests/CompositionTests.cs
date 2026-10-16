using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Cli.Tests;

public class CompositionTests
{
    [Fact]
    public void DI_Resolves_ReviewPipeline()
    {
        var services = Composition.BuildServices(".");
        Assert.NotNull(services.GetRequiredService<ReviewPipeline>());
    }

    [Fact]
    public void Configure_Override_Is_Applied()
    {
        var sp = Composition.BuildServices(".", o => o.Council.PromptMode = PromptMode.Naive);
        Assert.Equal(PromptMode.Naive, sp.GetRequiredService<TurtleOptions>().Council.PromptMode);
    }

    [Fact]
    public void Environment_Cannot_Switch_Product_Review_Into_Naive_Prompt_Mode()
    {
        const string key = "Turtle__Council__PromptMode";
        var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "Naive");
            var sp = Composition.BuildServices(".");
            Assert.Equal(PromptMode.Grounded, sp.GetRequiredService<TurtleOptions>().Council.PromptMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prior);
        }
    }
}
