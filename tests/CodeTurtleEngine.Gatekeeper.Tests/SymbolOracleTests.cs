using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class SymbolOracleTests
{
    private const string Source = """
        namespace Demo
        {
            public class Outer
            {
                public class Inner { }
                public void Run(int x) { }
                public T Echo<T>(T value) => value;
            }
            public class Box<T> { public T Value = default!; }
        }
        """;

    private static readonly SymbolOracle Oracle = new(CSharpCompilation.Create("Demo",
        new[] { CSharpSyntaxTree.ParseText(Source) },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }));

    [Theory]
    [InlineData("Demo.Outer")]
    [InlineData("global::Demo.Outer")]
    [InlineData("Demo.Outer.Run")]
    [InlineData("Demo.Outer.Run(int)")]
    [InlineData("Demo.Outer.Echo<T>")]
    [InlineData("Demo.Outer.Inner")]
    [InlineData("Demo.Box<int>")]
    [InlineData("Demo.Box<T>.Value")]
    [InlineData("System.String")]
    [InlineData("string")]
    [InlineData("string?")]
    [InlineData("int[]")]
    [InlineData("System.Collections.Generic.List<string>")]
    [InlineData("System.Collections.Generic.Dictionary<string, int>.TryGetValue")]
    public void Existing_Symbols_In_Any_Common_Display_Format_Exist(string fqn) =>
        Assert.True(Oracle.Exists(fqn), fqn);

    [Theory]
    [InlineData("Demo.Ghost")]
    [InlineData("Demo.Outer.Fly")]
    [InlineData("Demo.Outer.Inner.Deeper")]
    [InlineData("System.Net.Http.HttpClientFactoryOfDoom")]
    [InlineData("")]
    [InlineData("   ")]
    public void Invented_Symbols_Do_Not_Exist(string fqn) =>
        Assert.False(Oracle.Exists(fqn), fqn);
}
