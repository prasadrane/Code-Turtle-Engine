using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class TurtleShellGuardTests
{
    private static RoslynPayload PayloadWith(string resolvedSymbol) => new("R", "HEAD", new[]
    {
        new FileFacts("F.cs", new[]
        {
            new MethodFacts("M", Array.Empty<string>(), null, Array.Empty<string>(), new[] { resolvedSymbol })
        })
    });

    private static ReviewFinding Finding(string title, params string[] cites) =>
        new(PersonaRole.SecurityAuditor, Severity.Error, title, "d", "F.cs:1", cites);

    [Fact]
    public void Strip_Removes_Hallucinated_Citations_And_Ungrounded_Findings()
    {
        var findings = new[]
        {
            Finding("Real issue", "SampleRepo.Real"),
            Finding("Hallucinated", "SampleRepo.Fake"),
            Finding("Mixed", "SampleRepo.Real", "SampleRepo.Fake")
        };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Equal(2, outcome.KeptFindings.Count);
        Assert.Contains(outcome.KeptFindings, f => f.Title == "Real issue");
        Assert.DoesNotContain(outcome.KeptFindings, f => f.Title == "Hallucinated");
        Assert.Equal(new[] { "SampleRepo.Real" }, outcome.KeptFindings.First(f => f.Title == "Mixed").CitedSymbolFqns);
        Assert.Contains(outcome.Audit, a => a.CitedFqn == "SampleRepo.Fake" && !a.Verified);
    }

    [Fact]
    public void Flag_Keeps_Findings_And_Records_Audit()
    {
        var findings = new[] { Finding("Hallucinated", "SampleRepo.Fake") };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Flag });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Single(outcome.KeptFindings);
        Assert.Contains(outcome.Audit, a => a.CitedFqn == "SampleRepo.Fake" && !a.Verified);
    }

    [Fact]
    public void Finding_With_No_Citations_Is_Kept_In_Strip_Mode()
    {
        var findings = new[] { Finding("General note") };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Single(outcome.KeptFindings);
    }
}
