using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class PayloadGeneratorTests(SampleRepoCompilationFixture fx) : IClassFixture<SampleRepoCompilationFixture>
{
    [Fact]
    public void Builds_Payload_For_Changed_File()
    {
        var payload = new PayloadGenerator(fx.Loaded.Compilation)
            .Build("SampleRepo", "HEAD", new[] { "PaymentService.cs" });

        Assert.Equal("SampleRepo", payload.RepoSlug);
        Assert.Single(payload.Files);
        Assert.Equal("PaymentService.cs", payload.Files[0].FilePath);
        Assert.Contains(payload.Files[0].Methods, m => m.Method == "ProcessPaymentAsync");
    }

    [Fact]
    public void Skips_Files_Not_In_Compilation()
    {
        var payload = new PayloadGenerator(fx.Loaded.Compilation)
            .Build("SampleRepo", "HEAD", new[] { "DoesNotExist.cs" });

        Assert.Empty(payload.Files);
    }
}
