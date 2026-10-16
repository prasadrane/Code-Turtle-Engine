using System.Text.Json;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Core.Tests;

public class PayloadJsonTests
{
    [Fact]
    public void RoslynPayload_RoundTrips_Fields()
    {
        var payload = new RoslynPayload("SampleRepo", "HEAD~1..HEAD", new[]
        {
            new FileFacts("PaymentService.cs", new[]
            {
                new MethodFacts("ProcessPaymentAsync",
                    new[] { "LINQ Closure (Line 18)", "Implicit Boxing (Line 21)" },
                    "Missing ConfigureAwait(false) on IPaymentGateway invocation",
                    new[] { "IPaymentGateway", "ILogger" },
                    new[] { "SampleRepo.IPaymentGateway", "SampleRepo.ILogger" })
            })
        });

        var json = JsonSerializer.Serialize(payload, TurtleJson.Options);
        var back = JsonSerializer.Deserialize<RoslynPayload>(json, TurtleJson.Options)!;

        Assert.Equal("ProcessPaymentAsync", back.Files[0].Methods[0].Method);
        Assert.Equal(2, back.Files[0].Methods[0].Allocations.Count);
        Assert.Equal("SampleRepo.IPaymentGateway", back.Files[0].Methods[0].ResolvedSymbols[0]);
        Assert.Contains("\"Method\":\"ProcessPaymentAsync\"", json);
    }

    [Fact]
    public void Enums_Serialize_As_Strings()
    {
        var f = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection",
            "Raw concatenation builds a WHERE clause.", "DataAccess.cs:11",
            new[] { "SampleRepo.DataAccess.BuildQuery" });

        var json = JsonSerializer.Serialize(f, TurtleJson.Options);

        Assert.Contains("\"Persona\":\"SecurityAuditor\"", json);
        Assert.Contains("\"Severity\":\"Error\"", json);
    }

    [Fact]
    public void MethodFacts_RoundTrips_LineSpan_And_Risks()
    {
        var m = new MethodFacts(
            "PayAsync",
            new[] { "Implicit Boxing (Line 20)" },
            "Missing ConfigureAwait(false) on IPaymentGateway invocation (Line 19)",
            new[] { "IPaymentGateway" },
            new[] { "Shop.Infrastructure.IPaymentGateway.ChargeAsync" },
            StartLine: 17,
            EndLine: 23,
            Risks: new[] { "Unawaited Task (fire-and-forget) on IAuditLog.WriteAsync (Line 20)" });

        var json = JsonSerializer.Serialize(m, TurtleJson.Options);
        var back = JsonSerializer.Deserialize<MethodFacts>(json, TurtleJson.Options)!;

        Assert.Equal(17, back.StartLine);
        Assert.Equal(23, back.EndLine);
        Assert.NotNull(back.Risks);
        Assert.Single(back.Risks!);
        Assert.Contains("Line 20", back.Risks![0]);
    }
}
