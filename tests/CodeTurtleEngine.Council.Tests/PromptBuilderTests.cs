using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Council.Tests;

public class PromptBuilderTests
{
    private static RoslynPayload SamplePayload() => new("R", "HEAD", new[]
    {
        new FileFacts("DataAccess.cs", new[]
        {
            new MethodFacts("BuildQuery", Array.Empty<string>(), null,
                Array.Empty<string>(), new[] { "SampleRepo.DataAccess" })
        })
    });

    [Fact]
    public void RubricLoader_Reads_File()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "# Review Rubric v1\nBe specific.");
        var text = new RubricLoader().Load(tmp);
        Assert.Contains("Review Rubric", text);
    }

    [Fact]
    public void Security_Prompt_Has_System_Rubric_And_Payload()
    {
        var msgs = PromptBuilder.Build(PersonaRole.SecurityAuditor, SamplePayload(), "RUBRIC-TEXT");

        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("security", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("RUBRIC-TEXT"));
        Assert.Contains(msgs, m => m.Role == ChatRole.User && m.Text.Contains("DataAccess.cs"));
    }

    [Fact]
    public void VerdictSchema_Requires_Findings()
        => Assert.Contains("\"Findings\"", PromptBuilder.VerdictSchema);

    [Fact]
    public void System_Prompt_Contains_Findings_Cap()
    {
        var msgs = PromptBuilder.Build(PersonaRole.SecurityAuditor, SamplePayload(), "RUBRIC-TEXT", 7);
        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("AT MOST 7 findings"));
    }

    [Fact]
    public void ThreeArg_Build_Defaults_Findings_Cap()
    {
        var msgs = PromptBuilder.Build(PersonaRole.SecurityAuditor, SamplePayload(), "RUBRIC-TEXT");
        Assert.Contains(msgs, m => m.Role == ChatRole.System && m.Text.Contains("AT MOST 5 findings"));
    }

    [Fact]
    public void BuildNaive_Sends_Source_Not_Payload_And_Omits_Grounding_Rules()
    {
        var sources = new[] { new SourceFile("src/PaymentService.cs", "public class PaymentService { }") };

        var msgs = PromptBuilder.BuildNaive(PersonaRole.SecurityAuditor, sources, "# Rubric", 5);

        Assert.Equal(2, msgs.Count);
        var system = msgs[0].Text;
        var user = msgs[1].Text;
        Assert.Contains("security auditor", system);
        Assert.Contains("# Rubric", system);
        Assert.Contains("CitedSymbolFqns", system);
        Assert.Contains("AT MOST 5 findings", system);
        Assert.DoesNotContain("ResolvedSymbols", system);
        Assert.DoesNotContain("payload", system, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## File: src/PaymentService.cs", user);
        Assert.Contains("public class PaymentService { }", user);
    }

    [Fact]
    public void BuildNaive_Prefixes_Source_Lines_With_Line_Numbers()
    {
        var sources = new[] { new SourceFile("A.cs", "first\nsecond") };

        var msgs = PromptBuilder.BuildNaive(PersonaRole.SecurityAuditor, sources, "r", 5);

        Assert.Contains("   1: first", msgs[1].Text);
        Assert.Contains("   2: second", msgs[1].Text);
        Assert.Contains("prefixed with their line numbers", msgs[0].Text);
        Assert.Contains("File.cs:LINE", msgs[0].Text);
    }

    [Fact]
    public void CouncilOptions_Defaults_To_Grounded() =>
        Assert.Equal(PromptMode.Grounded, new CouncilOptions().PromptMode);
}
