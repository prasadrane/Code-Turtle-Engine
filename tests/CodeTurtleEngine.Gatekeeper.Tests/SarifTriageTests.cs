using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class SarifTriageTests
{
    private const string SampleSarif = """
    {
      "$schema": "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json",
      "version": "2.1.0",
      "runs": [
        {
          "tool": {
            "driver": { "name": "Microsoft.CodeAnalysis.NetAnalyzers" }
          },
          "results": [
            {
              "ruleId": "CA3001",
              "level": "error",
              "message": { "text": "Review code for SQL injection vulnerabilities" },
              "locations": [
                {
                  "physicalLocation": {
                    "artifactLocation": { "uri": "src/PaymentService.cs" },
                    "region": { "startLine": 15, "endLine": 15 }
                  }
                }
              ]
            },
            {
              "ruleId": "CA2007",
              "level": "warning",
              "message": { "text": "Consider calling ConfigureAwait on the awaited task" },
              "locations": [
                {
                  "physicalLocation": {
                    "artifactLocation": { "uri": "src/Untouched.cs" },
                    "region": { "startLine": 88, "endLine": 88 }
                  }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Parses_Sarif_Diagnostics_From_Json()
    {
        var diagnostics = SarifTriagePipeline.ParseSarifJson(SampleSarif);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal("CA3001", diagnostics[0].RuleId);
        Assert.Equal("src/PaymentService.cs", diagnostics[0].FilePath);
        Assert.Equal(15, diagnostics[0].StartLine);
        Assert.Equal("error", diagnostics[0].Level);
    }

    [Fact]
    public void Filters_Diagnostics_To_Changed_Diff_Hunks_Only()
    {
        var diagnostics = SarifTriagePipeline.ParseSarifJson(SampleSarif);
        var hunks = new[] { new DiffHunk("src/PaymentService.cs", 10, 10) }; // Lines 10-19

        var filtered = SarifTriagePipeline.IntersectWithDiffHunks(diagnostics, hunks);

        Assert.Single(filtered);
        Assert.Equal("CA3001", filtered[0].RuleId);
    }

    [Fact]
    public void Deduplicates_Across_Multi_Target_Framework_Runs()
    {
        var diag1 = new SarifDiagnostic("CA3001", "Msg", "src/A.cs", 10, 10, "error");
        var diag2 = new SarifDiagnostic("CA3001", "Msg", "src/A.cs", 10, 10, "error");
        var diag3 = new SarifDiagnostic("CA3001", "Different msg", "src/A.cs", 20, 20, "error");

        var deduped = SarifTriagePipeline.Deduplicate(new[] { diag1, diag2, diag3 });

        Assert.Equal(2, deduped.Count);
    }

    [Fact]
    public void Maps_Diagnostics_To_Review_Findings_With_Severity()
    {
        var diag = new SarifDiagnostic("CA3001", "SQL injection risk", "src/A.cs", 12, 12, "error");
        var findings = SarifTriagePipeline.ConvertToFindings(new[] { diag });

        Assert.Single(findings);
        Assert.Equal(Severity.Error, findings[0].Severity);
        Assert.Equal("CA3001", findings[0].Title);
        Assert.Equal("src/A.cs:12", findings[0].Location);
    }
}
