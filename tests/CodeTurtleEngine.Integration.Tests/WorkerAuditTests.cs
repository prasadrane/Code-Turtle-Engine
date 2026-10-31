using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Worker;

namespace CodeTurtleEngine.Integration.Tests;

public class WorkerAuditTests
{
    [Fact]
    public async Task WorkerAudit_WithNoDiffHunks_NeverSilentlyReturnsZeroFindings()
    {
        var loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.SampleRepoCsproj);
        try
        {
            var finding = new ReviewFinding(
                PersonaRole.SecurityAuditor,
                Severity.Warning,
                "Authored finding",
                "detail",
                "PaymentService.cs:15",
                new[] { "SampleRepo.IPaymentGateway" });

            // 1. When hunk guard is opt-in (false, default), findings stay
            var defaultOptions = new CouncilOptions { Guard = GuardMode.Strip, EnableHunkIntersectionGuard = false };
            var requestWithoutHunks = new WorkerRequest(
                Operation: "Audit",
                Project: "SampleRepo.csproj",
                ChangedFiles: new[] { "PaymentService.cs" },
                Findings: new[] { finding },
                Guard: GuardMode.Strip,
                Strict: false,
                DiffHunks: null);

            var resultDefault = WorkerAuditRunner.ExecuteAudit(requestWithoutHunks, loaded, "hash1", defaultOptions);
            Assert.NotEmpty(resultDefault.Outcome.KeptFindings);
            Assert.Contains(resultDefault.Diagnostics, d => d.Contains("Diff hunk evidence unavailable"));

            // 2. When hunk guard is enabled and findings are stripped, missing-evidence diagnostic MUST be reported
            var strictOptions = new CouncilOptions { Guard = GuardMode.Strip, EnableHunkIntersectionGuard = true };
            var resultStrict = WorkerAuditRunner.ExecuteAudit(requestWithoutHunks, loaded, "hash2", strictOptions);
            Assert.Empty(resultStrict.Outcome.KeptFindings);
            Assert.Contains(resultStrict.Diagnostics, d => d.Contains("Diff hunk evidence unavailable: strict L1 will reject ungrounded findings."));
            Assert.Contains("Diff hunk evidence unavailable", resultStrict.Markdown);

            // 3. When DiffHunks ARE provided, findings stay and no missing-evidence diagnostic is added
            var requestWithHunks = new WorkerRequest(
                Operation: "Audit",
                Project: "SampleRepo.csproj",
                ChangedFiles: new[] { "PaymentService.cs" },
                Findings: new[] { finding },
                Guard: GuardMode.Strip,
                Strict: false,
                DiffHunks: new[] { new DiffHunk("PaymentService.cs", 1, 100) });

            var resultWithHunks = WorkerAuditRunner.ExecuteAudit(requestWithHunks, loaded, "hash3", strictOptions);
            Assert.NotEmpty(resultWithHunks.Outcome.KeptFindings);
            Assert.DoesNotContain(resultWithHunks.Diagnostics, d => d.Contains("Diff hunk evidence unavailable"));
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }
}
