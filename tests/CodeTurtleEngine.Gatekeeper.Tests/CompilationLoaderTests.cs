using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class CompilationLoaderTests
{
    [Fact]
    public async Task Loads_SampleRepo_And_Resolves_PaymentService()
    {
        var loader = new MsBuildCompilationLoader();
        var loaded = await loader.LoadAsync(TestPaths.SampleRepoCsproj);

        var sym = loaded.Compilation.GetTypeByMetadataName("SampleRepo.PaymentService");
        Assert.NotNull(sym);
    }

    [Fact]
    public async Task Loads_MultiProject_Sln_And_Includes_Trees_From_All_Projects()
    {
        var loader = new MsBuildCompilationLoader();
        var loaded = await loader.LoadAsync(TestPaths.MultiProjectSln);

        var treePaths = loaded.Compilation.SyntaxTrees.Select(t => t.FilePath).ToList();
        Assert.Contains(treePaths, p => p.EndsWith("ServiceA.cs"));
        Assert.Contains(treePaths, p => p.EndsWith("ServiceB.cs"));
    }
}
