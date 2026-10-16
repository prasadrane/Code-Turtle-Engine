using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class SemanticAnalyzerTests(SampleRepoCompilationFixture fx) : IClassFixture<SampleRepoCompilationFixture>
{
    private SyntaxTree PaymentTree() =>
        fx.Loaded.Compilation.SyntaxTrees.First(t => t.FilePath.EndsWith("PaymentService.cs"));

    [Fact]
    public void Detects_Dependencies_And_ResolvedSymbols()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.Contains("IPaymentGateway", m.Dependencies);
        Assert.Contains("ILogger", m.Dependencies);
        Assert.Contains("SampleRepo.IPaymentGateway", m.ResolvedSymbols);
    }

    [Fact]
    public void Detects_Missing_ConfigureAwait()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.NotNull(m.AsyncHealth);
        Assert.Contains("ConfigureAwait", m.AsyncHealth);
    }

    [Fact]
    public void Detects_Boxing_And_Linq_Closure_Allocations()
    {
        var facts = new SemanticAnalyzer(fx.Loaded.Compilation).AnalyzeTree(PaymentTree());
        var m = facts.First(f => f.Method == "ProcessPaymentAsync");

        Assert.Contains(m.Allocations, a => a.Contains("Boxing"));
        Assert.Contains(m.Allocations, a => a.Contains("LINQ Closure"));
    }

    [Fact]
    public void Records_StartLine_And_EndLine_And_Walks_Constructors_And_Properties()
    {
        const string src = """
        namespace Demo;
        public class Sample
        {
            private readonly string _s;
            public Sample(string s)
            {
                _s = s;
            }
            public string Value => _s.Trim();
            public void Execute()
            {
                _ = _s.Length;
            }
        }
        """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(src, path: "Sample.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Demo", new[] { tree }, new[] { mscorlib });

        var facts = new SemanticAnalyzer(comp).AnalyzeTree(tree);

        Assert.Contains(facts, f => f.Method == "Sample" && f.StartLine == 5 && f.EndLine == 8);
        Assert.Contains(facts, f => f.Method == "Value" && f.StartLine == 9 && f.EndLine == 9);
        Assert.Contains(facts, f => f.Method == "Execute" && f.StartLine == 10 && f.EndLine == 13);
    }

    [Fact]
    public void Does_Not_Flag_Pure_Linq_Lambda_As_Closure_Capture()
    {
        const string src = """
        using System.Collections.Generic;
        using System.Linq;
        namespace Demo;
        public class Pure
        {
            public int CountPositive(IEnumerable<int> items)
            {
                return items.Where(x => x > 0).Count();
            }
        }
        """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(src, path: "Pure.cs");
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Demo", new[] { tree }, refs);

        var facts = new SemanticAnalyzer(comp).AnalyzeTree(tree);
        var m = facts.First(f => f.Method == "CountPositive");

        Assert.Empty(m.Allocations);
    }

    [Fact]
    public void ResolvedSymbols_Includes_Constructor_Fqn_With_Class_Name()
    {
        const string src = """
        namespace Demo;
        public class Sample
        {
            public Sample() { }
        }
        """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(src, path: "Sample.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Demo", new[] { tree }, new[] { mscorlib });

        var facts = new SemanticAnalyzer(comp).AnalyzeTree(tree);
        var m = facts.First(f => f.Method == "Sample");

        Assert.Contains("Demo.Sample.Sample", m.ResolvedSymbols);
    }
}


