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

        var treePaths = loaded.Projects.SelectMany(p => p.SyntaxTrees).Select(t => t.FilePath).ToList();
        Assert.Contains(treePaths, p => p.EndsWith("ServiceA.cs"));
        Assert.Contains(treePaths, p => p.EndsWith("ServiceB.cs"));
    }

    [Fact]
    public async Task Loads_MultiProject_Sln_Deduplicates_Metadata_References()
    {
        var loader = new MsBuildCompilationLoader();
        var loaded = await loader.LoadAsync(TestPaths.MultiProjectSln);

        var refDisplays = loaded.Projects[0].References.Select(r => r.Display).Where(d => !string.IsNullOrEmpty(d)).ToList();
        var duplicates = refDisplays.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }
    [Fact]
    public async Task Project_References_Keep_Their_Owning_Semantic_Model()
    {
        var loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.MultiProjectSln);
        using (loaded.Workspace)
        {
            Assert.Equal(2, loaded.Projects.Count);
            Assert.True(new SymbolOracle(loaded.Projects).Exists("MultiProject.ProjA.ServiceA.Greet"));
            var project = loaded.Projects.Single(c => c.AssemblyName == "ProjB");
            var tree = project.SyntaxTrees.Single(t => t.FilePath.EndsWith("ServiceB.cs"));
            Assert.Contains("MultiProject.ProjA.ServiceA.Greet",
                new SemanticAnalyzer(project).AnalyzeTree(tree).Single().ResolvedSymbols);
            Assert.DoesNotContain(project.SyntaxTrees, t => t.FilePath.EndsWith("ServiceA.cs"));
        }
    }
}

