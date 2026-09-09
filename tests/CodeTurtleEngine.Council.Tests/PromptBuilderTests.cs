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
}
