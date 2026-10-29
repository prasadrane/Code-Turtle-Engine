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

    [Fact]
    public void BuildAllowList_Collects_All_Resolved_Symbols_Ordinal()
    {
        var payload = new RoslynPayload("r", "HEAD", new[]
        {
            new FileFacts("A.cs", new[]
            {
                new MethodFacts("M1", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "Demo.A", "Demo.B" }),
                new MethodFacts("M2", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "Demo.B" })
            })
        });

        var allow = TurtleShellGuard.BuildAllowList(payload);

        Assert.Equal(2, allow.Count);
        Assert.Contains("Demo.A", allow);
        Assert.DoesNotContain("demo.a", allow);
    }

    [Fact]
    public void Verify_Accepts_Member_Citations_And_Oracle_Fallback()
    {
        var payload = PayloadWith("SampleRepo.IPaymentGateway.ChargeAsync");
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip });
        var findings = new[]
        {
            Finding("Exact member", "SampleRepo.IPaymentGateway.ChargeAsync"),
            Finding("Parent type of member", "SampleRepo.IPaymentGateway"),
            Finding("Oracle verified type", "System.Threading.Tasks.Task")
        };

        var outcome = guard.Verify(findings, payload, cite => cite is "System.Threading.Tasks.Task" or "SampleRepo.IPaymentGateway" or "SampleRepo.IPaymentGateway.ChargeAsync");

        Assert.Equal(3, outcome.KeptFindings.Count);
        Assert.All(outcome.Audit, a => Assert.True(a.Verified));
    }

    [Fact]
    public void Strip_Drops_Uncited_Finding_When_RequireCitationsInStripMode_Is_True()
    {
        var findings = new[] { Finding("Uncited advisory") };
        var guard = new TurtleShellGuard(new CouncilOptions
        {
            Guard = GuardMode.Strip,
            RequireCitationsInStripMode = true
        });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Empty(outcome.KeptFindings);
    }
    [Fact]
    public void Strip_Drops_Finding_When_Location_Does_Not_Intersect_Payload_Files()
    {
        var findings = new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "Off-file", "detail", "Untouched.cs:10", new[] { "SampleRepo.Real" })
        };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip, EnableHunkIntersectionGuard = true });

        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Drops_Finding_When_Line_Is_Outside_Payload_Method_Ranges()
    {
        var payload = new RoslynPayload("R", "HEAD", new[]
        {
            new FileFacts("F.cs", new[]
            {
                new MethodFacts("M", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "SampleRepo.Real" }, StartLine: 10, EndLine: 20)
            })
        });
        var findings = new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "Off-line", "detail", "F.cs:50", new[] { "SampleRepo.Real" })
        };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip, EnableHunkIntersectionGuard = true });

        var outcome = guard.Verify(findings, payload);

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Drops_Suggestion_When_OriginalCode_Does_Not_Match_Source()
    {
        var payload = new RoslynPayload("R", "HEAD", new[]
        {
            new FileFacts("F.cs", new[]
            {
                new MethodFacts("M", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "SampleRepo.Real" }, StartLine: 10, EndLine: 20)
            })
        });
        var findings = new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "Bad quote", "detail", "F.cs:15", new[] { "SampleRepo.Real" })
            {
                OriginalCode = "int a = 1;",
                SuggestedCode = "int a = 2;"
            }
        };
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip, EnableQuoteVerificationGuard = true });

        // Source resolver returns mismatched code
        var outcome = guard.Verify(findings, payload, sourceResolver: (file, start, end) => "string a = \"mismatch\";");

        Assert.Single(outcome.KeptFindings);
        Assert.Null(outcome.KeptFindings[0].SuggestedCode); // Cleared because quote did not match
        Assert.Equal("QuoteMismatch", outcome.FindingAudit.Single().Status);
    }
}
