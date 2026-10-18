using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class CompilationQualityTests
{
    [Fact]
    public void Separate_Projects_Preserve_Options_References_And_Duplicate_Names()
    {
        var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        CSharpCompilation Project(string name, bool enabled) => CSharpCompilation.Create(name,
            [CSharpSyntaxTree.ParseText("""
            namespace Shared;
            public class Duplicate {
            #if FEATURE
                public string Value() => "enabled";
            #else
                public int Value() => 1;
            #endif
            }
            """, new CSharpParseOptions(preprocessorSymbols: enabled ? ["FEATURE"] : []), path: name + ".cs")],
            [reference], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var a = Project("A", true);
        var b = Project("B", false);
        Assert.Empty(a.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(b.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(SpecialType.System_String, ((IMethodSymbol)a.GetTypeByMetadataName("Shared.Duplicate")!.GetMembers("Value").Single()).ReturnType.SpecialType);
        Assert.Equal(SpecialType.System_Int32, ((IMethodSymbol)b.GetTypeByMetadataName("Shared.Duplicate")!.GetMembers("Value").Single()).ReturnType.SpecialType);
        Assert.False(new SymbolOracle(new[] { a, b }).Exists("Shared.Duplicate.Value"));
        var payload = new PayloadGenerator(new[] { a, b }).Build("r", "HEAD", ["A.cs", "B.cs"]);
        Assert.Equal(2, payload.Files.Count);
        var consumerTree = CSharpSyntaxTree.ParseText("class Consumer { string M() => new Shared.Duplicate().Value(); }", path: "Consumer.cs");
        var consumer = CSharpCompilation.Create("Consumer", [consumerTree], [reference, a.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(consumer.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("Shared.Duplicate.Value", new SemanticAnalyzer(consumer).AnalyzeTree(consumerTree).Single().ResolvedSymbols);
    }

    [Fact]
    public void Error_Types_Are_Not_Resolved_Evidence()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { Missing field; void M() { field.NoSuchMethod(); } }", path: "C.cs");
        var compilation = CSharpCompilation.Create("Broken", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        Assert.DoesNotContain(new SemanticAnalyzer(compilation).AnalyzeTree(tree).SelectMany(f => f.ResolvedSymbols),
            name => name.Contains("Missing") || name.Contains("NoSuchMethod"));
        Assert.False(new SymbolOracle(compilation).Exists("Missing"));
        Assert.False(new SymbolOracle(compilation).Exists("C.field"));
    }

    [Fact]
    public async Task Missing_Dependency_Stops_Loading_Without_Restore()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-broken-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var project = Path.Combine(root, "Broken.csproj");
            await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(root, "C.cs"), "public class C { Missing.Dependency field; }");
            var error = await Assert.ThrowsAsync<CompilationException>(() => new MsBuildCompilationLoader().LoadAsync(project));
            Assert.Contains("Incomplete project", error.Message);
            Assert.False(File.Exists(Path.Combine(root, "obj", "project.assets.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
