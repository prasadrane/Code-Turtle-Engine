using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Council.Tests;

public class CitationEvidenceTests
{
    [Fact]
    public void Audit_Separates_Signature_Identity_Payload_Context_And_Retention()
    {
        var compilation = CSharpCompilation.Create("Demo", [CSharpSyntaxTree.ParseText("""
            namespace Demo;
            public class Changed { public void Run() { new Outside().Real(1); new Outside().Echo("x"); } }
            public class Outside { public void Real(int x) {} public void Unrelated() {} public T Echo<T>(T value) => value; }
            """, path: "Changed.cs")], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var payload = new PayloadGenerator(compilation).Build("demo", "fixture", ["Changed.cs"]);
        // Limit extraction to the changed member, as if the unrelated class were in another file.
        payload = payload with { Files = [payload.Files[0] with { Methods = [payload.Files[0].Methods[0]] }] };
        var oracle = new SymbolOracle(compilation);
        ReviewFinding Finding(string title, string citation) => new(PersonaRole.SecurityAuditor, Severity.Warning,
            title, "Authored test suggestion; correctness not verified", "Changed.cs:2", [citation]);
        var findings = new[] { Finding("valid", "Demo.Outside.Real(int)"),
            Finding("wrong overload", "Demo.Outside.Real(bool)"), Finding("unrelated", "Demo.Outside.Unrelated"),
            Finding("generic name", "Demo.Outside.Echo<System.String>") };
        var result = new TurtleShellGuard(new CouncilOptions()).Verify(findings, payload, citationResolver: oracle.Resolve);
        Assert.Equal(3, result.KeptFindings.Count);
        var valid = result.Audit.Single(a => a.CitedFqn.EndsWith("Real(int)"));
        Assert.Equal("Signature", valid.Resolution!.Match);
        Assert.Single(valid.Resolution.Symbols);
        var context = Assert.Single(valid.PayloadContexts!);
        Assert.Equal("Changed.cs", context.File);
        Assert.Equal("Run", context.Member);
        var rejected = result.Audit.Single(a => a.CitedFqn.EndsWith("Real(bool)"));
        Assert.False(rejected.Verified);
        Assert.Equal("Rejected", rejected.Resolution!.Match);
        Assert.True(result.Audit.Single(a => a.CitedFqn.EndsWith("Unrelated")).Verified);
        Assert.Empty(result.Audit.Single(a => a.CitedFqn.EndsWith("Unrelated")).PayloadContexts!);
        Assert.Single(result.Audit.Single(a => a.CitedFqn.Contains("Echo")).PayloadContexts!);
        Assert.Equal("Demo.Outside.Real(bool)", findings[1].CitedSymbolFqns.Single());
    }
}
