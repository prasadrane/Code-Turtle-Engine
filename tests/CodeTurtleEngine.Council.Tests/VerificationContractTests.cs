using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
namespace CodeTurtleEngine.Council.Tests;
public class VerificationContractTests
{
    [Theory]
    [InlineData(GuardMode.Strip, false, 4)]
    [InlineData(GuardMode.Strip, true, 3)]
    [InlineData(GuardMode.Flag, false, 5)]
    [InlineData(GuardMode.Flag, true, 5)]
    public void Policy_Matrix(GuardMode mode, bool strict, int kept)
    {
        var payload = new RoslynPayload("r", "HEAD", [new FileFacts("A.cs", [new MethodFacts("M", [], null, [], ["Demo.A.M"])])]);
        ReviewFinding Finding(string title, params string[] citations) =>
            new(PersonaRole.SecurityAuditor, Severity.Warning, title, "detail", "A.cs:1", citations);
        var findings = new[] { Finding("payload", "Demo.A.M"), Finding("broader", "System.String.Trim"),
            Finding("fake", "Demo.Fake"), Finding("mixed", "Demo.A.M", "Demo.Fake"), Finding("absent") };
        var result = new TurtleShellGuard(new CouncilOptions { Guard = mode, RequireCitationsInStripMode = strict })
            .Verify(findings, payload, name => name is "Demo.A.M" or "System.String.Trim");
        Assert.Equal(kept, result.KeptFindings.Count);
        var uncited = result.FindingAudit.Single(a => a.Original.Title == "absent");
        Assert.Equal("UncitedAdvisory", uncited.Status);
        Assert.Equal(mode == GuardMode.Flag || !strict, uncited.Retained);
        Assert.Contains(result.Audit, a => a.CitedFqn == "Demo.A.M" && a.Verified && a.Evidence == "Payload");
        Assert.Contains(result.Audit, a => a.CitedFqn == "System.String.Trim" && a.Verified && a.Evidence == "Compilation");
        Assert.Contains(result.Audit, a => a.CitedFqn == "Demo.Fake" && !a.Verified && a.Evidence == "Rejected");
        Assert.Equal(mode == GuardMode.Strip ? 1 : 2, result.KeptFindings.Single(f => f.Title == "mixed").CitedSymbolFqns.Count);
        Assert.Equal(2, findings[3].CitedSymbolFqns.Count);
    }
    [Fact]
    public void Oracle_Rejection_Overrides_Payload_And_Namespaces_Are_Not_Inferred()
    {
        var payload = new RoslynPayload("r", "HEAD", [new FileFacts("A.cs", [new MethodFacts("M", [], null, [], ["Demo.Sub.A"])])]);
        Assert.DoesNotContain("Demo.Sub", TurtleShellGuard.BuildAllowList(payload));
        var finding = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "x", "d", "A.cs:1", ["Demo.Sub.A"]);
        var result = new TurtleShellGuard(new CouncilOptions()).Verify([finding], payload, _ => false);
        Assert.Empty(result.KeptFindings);
        Assert.False(Assert.Single(result.Audit).Verified);
    }
}
