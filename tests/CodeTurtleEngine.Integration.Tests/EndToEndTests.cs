using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Integration.Tests;

public class EndToEndTests
{
    [Fact]
    public async Task Council_Is_Grounded_And_Guard_Strips_Hallucination()
    {
        var loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.SampleRepoCsproj);
        try
        {
            var payload = new PayloadGenerator(loaded.Compilation)
                .Build("SampleRepo", "HEAD", new[] { "PaymentService.cs" });

            var realSymbol = payload.Files
                .SelectMany(f => f.Methods)
                .SelectMany(m => m.ResolvedSymbols)
                .First(s => s.Contains("IPaymentGateway"));

            var options = new CouncilOptions { Quorum = 2, Guard = GuardMode.Strip };
            var verdicts = await new PersonaRunner(new CitingChat(realSymbol), options)
                .RunAsync(payload, "# Review Rubric v1");

            var merged = new Arbiter().Merge(verdicts);
            var guarded = new TurtleShellGuard(options).Verify(merged, payload);

            Assert.Contains(guarded.Audit, a => a.CitedFqn == realSymbol && a.Verified);
            Assert.Contains(guarded.Audit, a => a.CitedFqn == "SampleRepo.DoesNotExist" && !a.Verified);
            Assert.All(guarded.KeptFindings, f => Assert.DoesNotContain("SampleRepo.DoesNotExist", f.CitedSymbolFqns));
            Assert.Contains(guarded.KeptFindings, f => f.Title == "Async health");
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }
}
