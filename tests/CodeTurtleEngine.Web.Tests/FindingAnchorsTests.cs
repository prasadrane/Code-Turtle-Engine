using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Web.Tests;

public sealed class FindingAnchorsTests
{
    private const string Source = "partial class Service\n{\n    public int Read() => 1;\n}\n";
    private static readonly ReviewFinding Finding = new(PersonaRole.SecurityAuditor, Severity.Warning,
        "Inspect this", "Advisory", "Untrusted.cs:999", ["Service.Read"]);
    private static readonly MethodFacts Method = new("Read", [], null, [], ["Service.Read"], 3, 3);
    private static readonly ReviewChanges Changes = new(1, new("fixture/repo", 1, "Title", "base", "head", "base", "merge-base-to-head"),
        [new("Service.cs", "Service.cs", "Modified", true, Source, [])], []);

    [Fact]
    public void OmittedOverloadStillMakesMemberContextAmbiguous()
    {
        var payload = new RoslynPayload("fixture/repo", "base", [
            new("Service.cs", [Method]), new("TooLarge.cs", [Method with { StartLine = 8, EndLine = 8 }])]);

        Assert.Empty(FindingAnchors.Attach(Changes, payload, [Finding]).Anchors);
    }

    [Fact]
    public void CompilerEvidenceRejectsUnchangedOverload_AndDuplicateProjectType()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, path: "Service.cs");
        var payload = new RoslynPayload("fixture/repo", "base", [new("Service.cs", [Method])]);
        var project = CSharpCompilation.Create("Fixture", [tree]);
        Assert.Single(FindingAnchors.Attach(Changes, payload, [Finding], [project]).Anchors);

        var overload = CSharpSyntaxTree.ParseText("partial class Service { public int Read(int id) => id; }", path: "Unchanged.cs");
        Assert.Empty(FindingAnchors.Attach(Changes, payload, [Finding], [project.AddSyntaxTrees(overload)]).Anchors);

        var duplicate = CSharpCompilation.Create("OtherProject", [CSharpSyntaxTree.ParseText(Source, path: "Other.cs")]);
        Assert.Empty(FindingAnchors.Attach(Changes, payload, [Finding], [project, duplicate]).Anchors);
    }

    [Fact]
    public void CompilerEvidenceMustMatchPinnedSource_BeforeShowingMemberContext()
    {
        var tree = CSharpSyntaxTree.ParseText(Source.Replace("=> 1", "=> 2"), path: "Service.cs");
        var project = CSharpCompilation.Create("Fixture", [tree]);
        var payload = new RoslynPayload("fixture/repo", "base", [new("Service.cs", [Method])]);

        Assert.Empty(FindingAnchors.Attach(Changes, payload, [Finding], [project]).Anchors);
    }
}
