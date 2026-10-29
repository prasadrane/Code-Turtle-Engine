using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class RoslynToolServerTests
{
    private static LoadedCompilation CreateLoaded(string csharp)
    {
        var tree = CSharpSyntaxTree.ParseText(csharp, path: "Service.cs");
        var comp = CSharpCompilation.Create("TestLib",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        return new LoadedCompilation(comp, new Noop());
    }

    private sealed class Noop : IDisposable { public void Dispose() { } }

    [Fact]
    public async Task Finds_Definition_Of_Symbol()
    {
        var loaded = CreateLoaded("namespace Lib; public class Service { public void Execute() {} }");
        var server = new RoslynToolServer(loaded.Projects);

        var def = await server.GoToDefinitionAsync("Lib.Service.Execute");

        Assert.NotNull(def);
        Assert.Equal("Service.cs", def.FilePath);
        Assert.True(def.Line > 0);
    }

    [Fact]
    public async Task Gets_Type_Info_For_Exported_Type()
    {
        var loaded = CreateLoaded("namespace Lib; public interface IThing {} public class Service : IThing { public void Run() {} }");
        var server = new RoslynToolServer(loaded.Projects);

        var info = await server.GetTypeInfoAsync("Lib.Service");

        Assert.NotNull(info);
        Assert.Equal("Lib.Service", info.TypeName);
        Assert.Contains("Lib.IThing", info.Interfaces);
        Assert.Contains(info.Members, m => m.Contains("Run"));
    }

    [Fact]
    public async Task Searches_Text_Across_Syntax_Trees()
    {
        var loaded = CreateLoaded("namespace Lib; public class Service { string secret = \"magic_token\"; }");
        var server = new RoslynToolServer(loaded.Projects);

        var results = await server.SearchTextAsync("magic_token");

        Assert.Single(results);
        Assert.Equal("Service.cs", results[0].FilePath);
        Assert.Contains("magic_token", results[0].Snippet);
    }

    [Fact]
    public async Task Enforces_Max_Call_Budget()
    {
        var loaded = CreateLoaded("namespace Lib; public class Service {}");
        var server = new RoslynToolServer(loaded.Projects, maxBudget: 2);

        await server.GetTypeInfoAsync("Lib.Service");
        await server.GetTypeInfoAsync("Lib.Service");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => server.GetTypeInfoAsync("Lib.Service"));

        Assert.Contains("budget", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
