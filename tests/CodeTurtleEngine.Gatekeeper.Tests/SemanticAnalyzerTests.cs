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
}
