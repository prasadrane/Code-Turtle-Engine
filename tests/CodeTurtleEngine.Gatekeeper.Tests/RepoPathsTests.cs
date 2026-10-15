using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class RepoPathsTests
{
    [Theory]
    [InlineData("/repo/PaymentService.cs", "PaymentService.cs", true)]
    [InlineData(@"C:\repo\src\PaymentService.cs", "src/PaymentService.cs", true)]
    [InlineData("src/PaymentService.cs", "src/PaymentService.cs", true)]
    [InlineData("/repo/paymentservice.cs", "PaymentService.cs", true)]
    [InlineData("/repo/PaymentService.cs", "Service.cs", false)]
    [InlineData("/repo/PaymentService.cs", "./PaymentService.cs", true)]
    [InlineData("/repo/Other.cs", "PaymentService.cs", false)]
    public void EndsWith_Respects_Path_Segment_Boundaries(string path, string relative, bool expected) =>
        Assert.Equal(expected, RepoPaths.EndsWith(path, relative));
}
