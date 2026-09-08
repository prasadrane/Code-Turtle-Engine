using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class ArbiterTests
{
    [Fact]
    public void Merge_Dedupes_By_Title_Location_Keeping_Highest_Severity()
    {
        var verdicts = new[]
        {
            new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
            {
                new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "SQL injection", "d", "DataAccess.cs:11", new[] { "A" })
            }),
            new PersonaVerdict(PersonaRole.IdiomaticArchitect, new[]
            {
                new ReviewFinding(PersonaRole.IdiomaticArchitect, Severity.Error, "sql injection", "d2", "DataAccess.cs:11", new[] { "A" })
            })
        };

        var merged = new Arbiter().Merge(verdicts);

        Assert.Single(merged);
        Assert.Equal(Severity.Error, merged[0].Severity);
    }

    [Fact]
    public void RenderMarkdown_Includes_Title_Severity_Location()
    {
        var findings = new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection", "Raw concat.", "DataAccess.cs:11", new[] { "SampleRepo.DataAccess" })
        };

        var md = new Arbiter().RenderMarkdown(findings, new RoslynPayload("SampleRepo", "HEAD", Array.Empty<FileFacts>()));

        Assert.Contains("## [Error] SQL injection", md);
        Assert.Contains("DataAccess.cs:11", md);
    }
}
