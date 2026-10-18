using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;
namespace CodeTurtleEngine.Cli.Tests.Bench;

public class CitationCoverageTests
{
    [Fact]
    public void Coverage_Uses_Finding_Denominator_And_Keeps_Absent_Citations_Visible()
    {
        ReviewFinding Finding(params string[] names) => new(PersonaRole.SecurityAuditor, Severity.Warning, "t", "d", "A.cs:1", names);
        var score = BenchScorer.Score([Finding(), Finding("Demo.Real"), Finding("Demo.Real", "Demo.Fake")],
            new HashSet<string> { "Demo.Real" }, name => name == "Demo.Real", null, new Dictionary<string, PersonaRole>());
        Assert.Equal(2, score.CitedFindings);
        Assert.Equal(1, score.UncitedFindings);
        Assert.Equal(2.0 / 3, score.CitationCoverage);
        Assert.Equal(1, score.RejectedCitations);
        Assert.Null(score.Precision);
    }
    [Fact]
    public void Historical_Score_Without_Coverage_Does_Not_Invent_A_Denominator()
    {
        var score = new RunScore(1, 1, 0, 0, 0, null, null);
        Assert.Null(score.CitationCoverage);
        Assert.Null(score.UncitedFindings);
    }
}
