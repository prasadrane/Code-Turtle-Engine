using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class TurtleShellGuardTests
{
    private static RoslynPayload PayloadWith(string resolvedSymbol) => new("R", "HEAD", [
        new FileFacts("F.cs", [new MethodFacts("M", [], null, [], [resolvedSymbol])],
            DiffHunks: [new DiffHunk("F.cs", 1, 100)])
    ]);

    private static RoslynPayload PayloadWithRange(int start, int end) => new("R", "HEAD", [
        new FileFacts("F.cs", [new MethodFacts("M", [], null, [], ["SampleRepo.Real"], StartLine: start, EndLine: end)],
            DiffHunks: [new DiffHunk("F.cs", start, end - start + 1)])
    ]);

    private static ReviewFinding Finding(string title, params string[] cites) =>
        new(PersonaRole.SecurityAuditor, Severity.Error, title, "d", "F.cs:1", cites);

    private static ReviewFinding FindingAt(string location) =>
        new(PersonaRole.SecurityAuditor, Severity.Error, "Probe", "d", location, ["SampleRepo.Real"]);

    private static TurtleShellGuard Guard() =>
        new(new CouncilOptions { Guard = GuardMode.Strip, EnableHunkIntersectionGuard = true, EnableQuoteVerificationGuard = true });

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
        var payload = new RoslynPayload("r", "HEAD", [
            new FileFacts("A.cs", [
                new MethodFacts("M1", [], null, [], ["Demo.A", "Demo.B"]),
                new MethodFacts("M2", [], null, [], ["Demo.B"])
            ], DiffHunks: [new DiffHunk("A.cs", 1, 100)])
        ]);

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
        var guard = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip, RequireCitationsInStripMode = true });
        var outcome = guard.Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Empty(outcome.KeptFindings);
    }

    [Fact]
    public void Strip_Drops_Finding_When_Location_Does_Not_Intersect_Payload_Files()
    {
        var findings = new[] { FindingAt("Untouched.cs:10") };
        var outcome = Guard().Verify(findings, PayloadWith("SampleRepo.Real"));

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Drops_Finding_When_Line_Is_Outside_Payload_Hunk_Ranges()
    {
        var findings = new[] { FindingAt("F.cs:50") };
        var outcome = Guard().Verify(findings, PayloadWithRange(10, 20));

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Drops_Suggestion_When_OriginalCode_Does_Not_Match_Source()
    {
        var findings = new[] { FindingAt("F.cs:15") with { OriginalCode = "int a = 1;", SuggestedCode = "int a = 2;" } };
        var outcome = Guard().Verify(findings, PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "string a = \"mismatch\";");

        Assert.Single(outcome.KeptFindings);
        Assert.Null(outcome.KeptFindings[0].SuggestedCode);
        Assert.Equal("QuoteMismatch", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Rejects_Unmatched_File_Even_If_Citation_Matches_Payload()
    {
        var findings = new[] { FindingAt("Wrong.cs:999") };
        var outcome = Guard().Verify(findings, PayloadWithRange(10, 20));

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.Single().Status);
    }

    [Theory]
    [InlineData("F.cs:15-1")]
    [InlineData("F.cs:0")]
    [InlineData("F.cs:-5")]
    [InlineData("F.cs:")]
    [InlineData("F.cs:abc")]
    public void Strip_Rejects_Malformed_Missing_Zero_Negative_And_Reversed_Spans(string malformedLocation)
    {
        var findings = new[] { FindingAt(malformedLocation) };
        var outcome = Guard().Verify(findings, PayloadWithRange(10, 20));

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("MalformedLocation", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Enforces_Actual_Diff_Hunk_Boundaries_When_Present()
    {
        var payload = new RoslynPayload("R", "HEAD", [
            new FileFacts("F.cs", [new MethodFacts("M", [], null, [], ["SampleRepo.Real"], StartLine: 10, EndLine: 50)],
                DiffHunks: [new DiffHunk("F.cs", 10, 5)]) // lines 10..14
        ]);
        var findings = new[]
        {
            FindingAt("F.cs:12") with { Title = "In hunk" },
            FindingAt("F.cs:18") with { Title = "In method but off hunk" },
            FindingAt("F.cs:12-25") with { Title = "Span exceeding hunk" }
        };
        var outcome = Guard().Verify(findings, payload);

        Assert.Single(outcome.KeptFindings);
        Assert.Equal("In hunk", outcome.KeptFindings[0].Title);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.First(a => a.Original.Title == "In method but off hunk").Status);
        Assert.Equal("OffDiffLocation", outcome.FindingAudit.First(a => a.Original.Title == "Span exceeding hunk").Status);
    }

    [Fact]
    public void Strip_Drops_Suggestion_When_OriginalCode_Is_Missing_Or_Whitespace()
    {
        var findings = new[] { FindingAt("F.cs:15") with { OriginalCode = null, SuggestedCode = "replacement" } };
        var outcome = Guard().Verify(findings, PayloadWithRange(10, 20));

        Assert.Single(outcome.KeptFindings);
        Assert.Null(outcome.KeptFindings[0].SuggestedCode);
        Assert.Equal("MissingQuote", outcome.FindingAudit.Single().Status);
    }

    [Fact]
    public void Strip_Rejects_Findings_When_DiffHunks_Is_Null()
    {
        var payload = new RoslynPayload("R", "base", [
            new FileFacts("F.cs", [new MethodFacts("C.M", [], null, [], ["C.M"], StartLine: 10, EndLine: 20)], DiffHunks: null)
        ]);
        var finding = FindingAt("F.cs:15");
        var outcome = Guard().Verify([finding], payload);

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("NoDiffEvidence", Assert.Single(outcome.FindingAudit).Status);
    }

    [Fact]
    public void Strip_Rejects_Findings_When_DiffHunks_Is_Empty()
    {
        var payload = new RoslynPayload("R", "base", [
            new FileFacts("F.cs", [new MethodFacts("C.M", [], null, [], ["C.M"], StartLine: 10, EndLine: 20)], DiffHunks: [])
        ]);
        var finding = FindingAt("F.cs:15");
        var outcome = Guard().Verify([finding], payload);

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("NoChangedLines", Assert.Single(outcome.FindingAudit).Status);
    }

    [Fact]
    public void Strip_Rejects_Findings_When_Payload_Has_No_Files_While_Preserving_Citation_Audit()
    {
        var payload = new RoslynPayload("R", "base", []);
        var finding = FindingAt("F.cs:15");
        var outcome = Guard().Verify([finding], payload, _ => true);

        Assert.Empty(outcome.KeptFindings);
        Assert.Equal("NoDiffEvidence", Assert.Single(outcome.FindingAudit).Status);
        Assert.True(Assert.Single(outcome.Audit).Verified);
        Assert.Equal("Compilation", outcome.Audit[0].Evidence);
    }

    [Fact]
    public void QuoteVerification_Preserves_Multiline_Verbatim_Strings_And_Empty_Lines()
    {
        var finding = FindingAt("F.cs:15-18") with { OriginalCode = "var s = @\"a\n\n b\";", SuggestedCode = "var s = \"c\";" };
        var outcome1 = Guard().Verify([finding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\"a\nb\";");
        Assert.Single(outcome1.KeptFindings);
        Assert.Null(outcome1.KeptFindings[0].SuggestedCode);
        Assert.Equal("QuoteMismatch", outcome1.FindingAudit.Single().Status);

        var outcome2 = Guard().Verify([finding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\"a\n\n     b\";");
        Assert.Single(outcome2.KeptFindings);
        Assert.Null(outcome2.KeptFindings[0].SuggestedCode);
        Assert.Equal("QuoteMismatch", outcome2.FindingAudit.Single().Status);

        var outcome3 = Guard().Verify([finding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\"a\n\n b\";");
        Assert.Single(outcome3.KeptFindings);
        Assert.NotNull(outcome3.KeptFindings[0].SuggestedCode);
    }

    [Fact]
    public void QuoteVerification_Rejects_Leading_Trailing_And_Whitespace_Only_Literal_Differences()
    {
        var finding = FindingAt("F.cs:15") with { OriginalCode = "var s = @\"a\";", SuggestedCode = "replacement" };
        var leadingSpace = Guard().Verify([finding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\" a\";");
        Assert.Null(leadingSpace.KeptFindings[0].SuggestedCode);

        var trailingSpace = Guard().Verify([finding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\"a \";");
        Assert.Null(trailingSpace.KeptFindings[0].SuggestedCode);

        var whitespaceLineFinding = FindingAt("F.cs:15-17") with { OriginalCode = "var s = @\"a\n\nb\";", SuggestedCode = "replacement" };
        var whitespaceLine = Guard().Verify([whitespaceLineFinding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "var s = @\"a\n   \nb\";");
        Assert.Null(whitespaceLine.KeptFindings[0].SuggestedCode);

        var rawStringFinding = FindingAt("F.cs:15-17") with { OriginalCode = "\"\"\"\n    hello\n    \"\"\";", SuggestedCode = "replacement" };
        var rawMatch = Guard().Verify([rawStringFinding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "\"\"\"\n    hello\n    \"\"\";");
        Assert.NotNull(rawMatch.KeptFindings[0].SuggestedCode);

        var rawMismatch = Guard().Verify([rawStringFinding], PayloadWithRange(10, 20), sourceResolver: (_, _, _) => "\"\"\"\n      hello\n    \"\"\";");
        Assert.Null(rawMismatch.KeptFindings[0].SuggestedCode);
    }
}
