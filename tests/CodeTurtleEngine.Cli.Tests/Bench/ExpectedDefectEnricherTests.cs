using CodeTurtleEngine.Cli.Bench;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class ExpectedDefectEnricherTests
{
    private const string Source = """
        namespace Demo;
        public class Svc
        {
            private int _f = 0;
            public void Alpha()
            {
                var x = 1;
            }
            public int Beta() => 2;
        }
        """;

    private static readonly Compilation C = CSharpCompilation.Create("Demo",
        new[] { CSharpSyntaxTree.ParseText(Source, path: "/repo/src/Svc.cs") },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

    [Theory]
    [InlineData("src/Svc.cs", 7, "Alpha")]
    [InlineData("Svc.cs", 9, "Beta")]
    [InlineData("Svc.cs", 4, null)]
    [InlineData("Other.cs", 7, null)]
    public void Sets_Enclosing_Method(string file, int line, string? method)
    {
        var result = ExpectedDefectEnricher.Enrich(C, new[] { new ExpectedDefect(file, line, "X") });
        Assert.Equal(method, result![0].Method);
    }

    [Fact]
    public void Keeps_Explicit_Method_And_Passes_Null_Through()
    {
        var d = new ExpectedDefect("Svc.cs", 7, "X", "Manual");
        Assert.Equal("Manual", ExpectedDefectEnricher.Enrich(C, new[] { d })![0].Method);
        Assert.Null(ExpectedDefectEnricher.Enrich(C, null));
    }
}
