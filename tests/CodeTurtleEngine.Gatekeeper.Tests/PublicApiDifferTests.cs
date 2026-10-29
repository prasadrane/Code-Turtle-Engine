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

    [Fact]
    public void Detects_Changed_Parameter_Passing_Mode_Ref_Out_In()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { public void Process(int val, int count) {} }");
        var headRef = CreateCompilation("namespace Lib; public class Service { public void Process(ref int val, int count) {} }");
        var headOut = CreateCompilation("namespace Lib; public class Service { public void Process(int val, out int count) { count = 0; } }");

        var changesRef = PublicApiDiffer.Diff(baseComp, headRef);
        var changesOut = PublicApiDiffer.Diff(baseComp, headOut);

        Assert.Contains(changesRef, c => c.DiagnosticId == "CP0002" && c.SymbolFqn.Contains("Process"));
        Assert.Contains(changesOut, c => c.DiagnosticId == "CP0002" && c.SymbolFqn.Contains("Process"));
    }

    [Fact]
    public void Detects_Removed_Overload_When_Other_Overload_Remains()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { public void M(int x) {} public void M(string s) {} }");
        var headComp = CreateCompilation("namespace Lib; public class Service { public void M(string s) {} }");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Single(changes);
        Assert.Equal("CP0002", changes[0].DiagnosticId);
        Assert.Equal("Lib.Service.M", changes[0].SymbolFqn);
    }

    [Fact]
    public void Detects_Removed_Constructor()
    {
        var baseComp = CreateCompilation("namespace Lib; public class Service { public Service(int x) {} }");
        var headComp = CreateCompilation("namespace Lib; public class Service { public Service() {} }");

        var changes = PublicApiDiffer.Diff(baseComp, headComp);

        Assert.Single(changes);
        Assert.Equal("CP0002", changes[0].DiagnosticId);
        Assert.Equal("Lib.Service..ctor", changes[0].SymbolFqn);
    }
}
