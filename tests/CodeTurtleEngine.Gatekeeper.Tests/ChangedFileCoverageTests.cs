using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public sealed class ChangedFileCoverageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "turtle-coverage-" + Guid.NewGuid().ToString("N"));

    public ChangedFileCoverageTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Independent_Project_Changes_Cannot_Be_Silently_Omitted()
    {
        var first = Write("First/C.cs", "class C { void M() {} }");
        Write("Second/D.cs", "class D { void M() {} }");
        var compilation = CSharpCompilation.Create("First", [CSharpSyntaxTree.ParseText(File.ReadAllText(first), path: first)]);

        var error = Assert.Throws<CompilationException>(() => new PayloadGenerator(compilation)
            .Build("repo", "HEAD", ["First/C.cs", "Second/D.cs"], repositoryRoot: _root));

        Assert.Contains("Second/D.cs", error.Message);
        Assert.Contains("outside the loaded compilation", error.Message);
    }

    [Fact]
    public void Deleted_Files_And_Compiled_Files_Without_Executable_Members_Are_Allowed()
    {
        var path = Write("Contracts.cs", "enum ContractKind { Default }");
        var compilation = CSharpCompilation.Create("Contracts", [CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)]);

        var payload = new PayloadGenerator(compilation)
            .Build("repo", "HEAD", ["Deleted.cs", "Contracts.cs"], repositoryRoot: _root);

        Assert.Empty(payload.Files);
    }

    [Theory]
    [InlineData("../Outside.cs")]
    [InlineData("..\\Outside.cs")]
    public void Unsafe_Changed_Paths_Are_Rejected_Even_If_The_File_Is_Missing(string path)
    {
        Assert.Throws<CompilationException>(() => new PayloadGenerator(CSharpCompilation.Create("Empty"))
            .Build("repo", "HEAD", [path], repositoryRoot: _root));
    }

    [Fact]
    public void A_Directory_Is_Not_Treated_As_A_Deleted_Source_File()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Directory.cs"));
        Assert.Throws<CompilationException>(() => new PayloadGenerator(CSharpCompilation.Create("Empty"))
            .Build("repo", "HEAD", ["Directory.cs"], repositoryRoot: _root));
    }

    private string Write(string relative, string source)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
