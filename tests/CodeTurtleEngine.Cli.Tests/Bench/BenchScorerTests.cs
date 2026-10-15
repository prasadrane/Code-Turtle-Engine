using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class BenchScorerTests
{
    private static readonly IReadOnlySet<string> Allow = new HashSet<string>(StringComparer.Ordinal) { "Demo.Dep" };
    private static readonly HashSet<string> Real = new(StringComparer.Ordinal) { "Demo.Dep", "Demo.Outer" };
    private static readonly IReadOnlyDictionary<string, PersonaRole> Classes = new Dictionary<string, PersonaRole>
    {
        ["SqlInjection"] = PersonaRole.SecurityAuditor,
        ["MissingConfigureAwait"] = PersonaRole.AllocationsPerformance
    };

    private static ReviewFinding F(string location, PersonaRole persona = PersonaRole.SecurityAuditor,
        Severity severity = Severity.Error, params string[] cites) =>
        new(persona, severity, "t", "d", location, cites);

    private static RunScore Score(IReadOnlyList<ReviewFinding> fs, IReadOnlyList<ExpectedDefect>? expected = null) =>
        BenchScorer.Score(fs, Allow, Real.Contains, expected, Classes);

    [Fact]
    public void Counts_Unverified_And_Invented_Separately()
    {
        var s = Score(new[]
        {
            F("A.cs:1", cites: new[] { "Demo.Dep" }),                // verified + real
            F("A.cs:2", cites: new[] { "Demo.Outer", "Demo.Ghost" }), // unverified real + invented
            F("A.cs:3")                                              // no citations
        });

        Assert.Equal(3, s.Findings);
        Assert.Equal(3, s.Citations);
        Assert.Equal(2, s.Unverified);
        Assert.Equal(1, s.Invented);
        Assert.Equal(1, s.FindingsWithInvented);
        Assert.Equal(1.0 / 3, s.InventedRate, 6);
        Assert.Null(s.Matched);
        Assert.Null(s.Precision);
    }

    [Fact]
    public void Zero_Citations_Gives_Zero_Rates() =>
        Assert.Equal(0, Score(Array.Empty<ReviewFinding>()).InventedRate);

    [Theory]
    [InlineData("src/Repo.cs:42", true)]
    [InlineData("src/Repo.cs:45", true)]   // +3 boundary
    [InlineData("src/Repo.cs:39", true)]   // -3 boundary
    [InlineData("src/Repo.cs:46", false)]  // outside tolerance
    [InlineData(@"src\Repo.cs:42", true)]
    [InlineData("Repo.cs:42-50", true)]
    [InlineData("OtherRepo.cs:42", false)] // suffix collision is not a match
    [InlineData("src/Repo.cs", false)]     // no line
    [InlineData("line 42 of Repo.cs", false)]
    [InlineData("", false)]
    public void Matches_By_File_And_Line_Tolerance(string location, bool matched)
    {
        var s = Score(new[] { F(location) }, new[] { new ExpectedDefect("src/Repo.cs", 42, "SqlInjection") });
        Assert.Equal(matched ? 1 : 0, s.Matched);
    }

    [Fact]
    public void Persona_Must_Match_Class()
    {
        var s = Score(new[] { F("Repo.cs:42", PersonaRole.IdiomaticArchitect) },
            new[] { new ExpectedDefect("Repo.cs", 42, "SqlInjection") });
        Assert.Equal(0, s.Matched);
    }

    [Fact]
    public void One_Finding_Matches_At_Most_One_Defect_And_Duplicates_Are_False_Positives()
    {
        var s = Score(
            new[] { F("Repo.cs:42", severity: Severity.Warning), F("Repo.cs:43", severity: Severity.Critical) },
            new[] { new ExpectedDefect("Repo.cs", 42, "SqlInjection") });

        Assert.Equal(1, s.Matched);
        Assert.Equal(0.5, s.Precision);
        Assert.Equal(1.0, s.Recall);
    }

    [Fact]
    public void Empty_Expected_List_Has_Null_Recall_And_Counts_All_Findings_As_False_Positives()
    {
        var s = Score(new[] { F("Repo.cs:1") }, Array.Empty<ExpectedDefect>());
        Assert.Equal(0, s.Matched);
        Assert.Equal(0.0, s.Precision);
        Assert.Null(s.Recall);
    }
}
