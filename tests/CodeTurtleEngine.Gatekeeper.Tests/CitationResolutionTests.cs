using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class CitationResolutionTests
{
    private const string Source = """
        namespace Demo;
        public class Base { public void Inherited(int x) {} }
        public interface IWork { void Work(string value); }
        public class Target : Base, IWork
        {
            public Target(int x) {}
            public void Run(int x) {}
            public void Run(string x) {}
            public void ByRef(ref int x) {}
            public void Array(int[] x) {}
            public T Echo<T>(T x) => x;
            public void Work(string value) {}
        }
        public static class Extensions { public static void Extend(this Target t, int x) {} }
        """;

    internal static CSharpCompilation Compile(string assembly = "Fixture") => CSharpCompilation.Create(assembly,
        [CSharpSyntaxTree.ParseText(Source, path: "Target.cs")],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    [Theory]
    [InlineData("Demo.Target.Run(bool)")]
    [InlineData("Demo.Target.Run(int, string)")]
    [InlineData("Demo.Target.Run()")]
    [InlineData("Demo.Target.ByRef(int)")]
    [InlineData("Demo.Target.Array(int)")]
    [InlineData("Demo.Target.Run(int) trailing")]
    public void Fabricated_Signature_Cannot_Pass_By_Member_Name(string citation) =>
        Assert.False(new SymbolOracle(Compile()).Exists(citation));

    [Theory]
    [InlineData("Demo.Target.Run(int)")]
    [InlineData("global::Demo.Target.Run(System.String)")]
    [InlineData("Demo.Target.ByRef(ref int)")]
    [InlineData("Demo.Target.Array(int[])")]
    [InlineData("Demo.Target.Inherited(int)")]
    [InlineData("Demo.IWork.Work(string)")]
    [InlineData("Demo.Target.Target(int)")]
    [InlineData("Demo.Target..ctor(int)")]
    [InlineData("Demo.Target.Echo<T>(T)")]
    [InlineData("Demo.Extensions.Extend(Demo.Target, int)")]
    public void Exact_Declaration_Signature_Has_One_Identity(string citation)
    {
        var result = new SymbolOracle(Compile()).Resolve(citation);
        Assert.True(result.Exists);
        Assert.Equal("Signature", result.Match);
        var identity = Assert.Single(result.Symbols);
        Assert.Contains("Fixture", identity.Assembly);
        Assert.StartsWith("M:", identity.DocumentationId);
    }

    [Fact]
    public void Name_Only_Overload_Set_Does_Not_Claim_A_Signature()
    {
        var result = new SymbolOracle(Compile()).Resolve("Demo.Target.Run");
        Assert.True(result.Exists);
        Assert.Equal("NameOnly", result.Match);
        Assert.Equal(2, result.Symbols.Count);
    }

    [Fact]
    public void Same_Type_Name_In_Different_Assemblies_Fails_Closed()
    {
        var result = new SymbolOracle([Compile("One"), Compile("Two")]).Resolve("Demo.Target.Run(int)");
        Assert.False(result.Exists);
    }

    [Fact]
    public void Separate_Source_Projects_With_Same_Assembly_Name_Are_Still_Ambiguous() =>
        Assert.False(new SymbolOracle([Compile("Same"), Compile("Same")]).Exists("Demo.Target.Run(int)"));

    [Fact]
    public void Constructed_Generic_Method_Is_Not_Mistaken_For_Exact_Declaration() =>
        Assert.False(new SymbolOracle(Compile()).Exists("Demo.Target.Echo<int>(int)"));
}
