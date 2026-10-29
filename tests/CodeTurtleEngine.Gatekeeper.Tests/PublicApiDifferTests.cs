using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class PublicApiDifferTests
{
    private static Compilation CreateCompilation(string csharp)
    {
        var tree = CSharpSyntaxTree.ParseText(csharp);
        return CSharpCompilation.Create("TestLib",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
    }

    [Fact]
    public void Detects_Removed_Public_Type()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { }");
        var headComp = CreateCompilation("namespace Lib;");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Single(changes);
        Assert.Equal("CP0001", changes[0].DiagnosticId);
        Assert.Equal("Lib.Service", changes[0].SymbolFqn);
    }

    [Fact]
    public void Detects_Removed_Public_Method()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { public void DoWork() {} }");
        var headComp = CreateCompilation("namespace Lib; public class Service { }");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Single(changes);
        Assert.Equal("CP0002", changes[0].DiagnosticId);
        Assert.Equal("Lib.Service.DoWork", changes[0].SymbolFqn);
    }

    [Fact]
    public void Detects_Changed_Method_Return_Type()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { public int Calculate() => 42; }");
        var headComp = CreateCompilation("namespace Lib; public class Service { public string Calculate() => string.Empty; }");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Single(changes);
        Assert.Equal("CP0003", changes[0].DiagnosticId);
        Assert.Equal("Lib.Service.Calculate", changes[0].SymbolFqn);
    }

    [Fact]
    public void Ignores_Internal_Or_Private_Symbols()
    {
        var baseComp = CreateCompilation("namespace Lib; internal class InternalService { private void Hidden() {} }");
        var headComp = CreateCompilation("namespace Lib;");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Empty(changes);
    }
}
