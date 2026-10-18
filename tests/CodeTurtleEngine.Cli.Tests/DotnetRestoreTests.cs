using CodeTurtleEngine.Cli.Bench;
namespace CodeTurtleEngine.Cli.Tests;
public class DotnetRestoreTests
{
    [Fact]
    public async Task Cancellation_Before_Start_Propagates()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => DotnetRestore.RunAsync("unused.csproj", ct.Token));
    }
    [Fact]
    public async Task Trusted_Restore_Has_A_Deadline()
    {
        var ex = await Assert.ThrowsAsync<BenchException>(() =>
            DotnetRestore.RunAsync("missing-trusted-fixture.csproj", TimeSpan.FromMilliseconds(1), CancellationToken.None));
        Assert.Contains("exceeded", ex.Message);
    }
}
