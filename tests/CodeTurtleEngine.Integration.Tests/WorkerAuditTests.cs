using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Integration.Tests;

public class WorkerAuditTests
{
    [Fact]
    public void WorkerAudit_RejectsSameSuffixTreeOutsideThePinnedSourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-worker-coverage-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "C.cs"), "class Expected {} ");
            var wrongTree = CSharpSyntaxTree.ParseText("class Other {}", path: Path.Combine(root, "other", "C.cs"));
            using var workspace = new AdhocWorkspace();
            var loaded = new LoadedCompilation(CSharpCompilation.Create("Other", [wrongTree]), workspace);
            var request = new WorkerRequest("Audit", "Project.csproj", ["C.cs"], []);

            Assert.Throws<CompilationException>(() => WorkerAuditRunner.ExecuteAudit(request, loaded, "sealed-hash",
                repositoryRoot: root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
            Assert.Contains(resultStrict.Diagnostics, d => d.Contains(DiffEvidence.UnavailableDiagnostic));
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
