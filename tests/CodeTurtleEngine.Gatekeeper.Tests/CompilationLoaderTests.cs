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
    [Fact]
    public async Task Tolerates_Unchanged_Project_Errors_And_Marks_Degraded()
    {
        var temp = Path.Combine(Path.GetTempPath(), "turtle-multi-degraded-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.GetDirectoryName(TestPaths.MultiProjectSln)!, temp);
        try
        {
            var brokenFile = Path.Combine(temp, "ProjB", "ServiceB.cs");
            await File.WriteAllTextAsync(brokenFile, "namespace MultiProject.ProjB; public class ServiceB { MissingType field; }");
            var slnPath = Path.Combine(temp, "MultiProject.sln");

            var loader = new MsBuildCompilationLoader();
            var loaded = await loader.LoadAsync(slnPath, new[] { "ServiceA.cs" });

            using (loaded.Workspace)
            {
                Assert.Single(loaded.Projects);
                Assert.Equal("ProjA", loaded.Projects[0].AssemblyName);
                Assert.Contains("ProjB", loaded.DegradedProjects);
                Assert.Contains(loaded.Diagnostics, d => d.Contains("ProjB"));
            }
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public async Task Throws_When_Changed_Project_Has_Compilation_Errors()
    {
        var temp = Path.Combine(Path.GetTempPath(), "turtle-multi-throw-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.GetDirectoryName(TestPaths.MultiProjectSln)!, temp);
        try
        {
            var brokenFile = Path.Combine(temp, "ProjB", "ServiceB.cs");
            await File.WriteAllTextAsync(brokenFile, "namespace MultiProject.ProjB; public class ServiceB { MissingType field; }");
            var slnPath = Path.Combine(temp, "MultiProject.sln");

            var loader = new MsBuildCompilationLoader();
            var ex = await Assert.ThrowsAsync<CompilationException>(() => loader.LoadAsync(slnPath, new[] { "ServiceB.cs" }));
            Assert.Contains("ProjB", ex.Message);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                continue;
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destinationDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }
}
