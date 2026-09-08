using CodeTurtleEngine.Cli;
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
}
