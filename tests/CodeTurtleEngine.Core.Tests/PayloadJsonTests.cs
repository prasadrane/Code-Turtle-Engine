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
}
