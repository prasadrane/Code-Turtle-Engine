using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Core.Tests;

public class PullRequestHostTests
{
    [Fact]
    public void Fingerprint_Is_Stable_For_Same_Finding()
    {
        var f1 = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL Injection", "detail", "src/A.cs:10", new[] { "DB.Execute" });
        var f2 = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL Injection", "detail", "src/A.cs:10", new[] { "DB.Execute" });

        var hash1 = FindingFingerprint.Compute(f1);
        var hash2 = FindingFingerprint.Compute(f2);

        Assert.Equal(hash1, hash2);
        Assert.Equal(16, hash1.Length);
    }

    [Fact]
    public void Marker_Serializes_And_Deserializes_Correctly()
    {
        var metadata = new PullRequestMetadata("abc1234", new[] { "hash1", "hash2" });
        var marker = PullRequestMarker.Format(metadata);

        Assert.Contains("<!-- code-turtle-metadata:", marker);
        Assert.Contains("abc1234", marker);

        var parsed = PullRequestMarker.Parse("Some summary text\n\n" + marker);
        Assert.NotNull(parsed);
        Assert.Equal("abc1234", parsed.LastReviewedSha);
        Assert.Equal(2, parsed.ReviewedFindingHashes.Count);
    }

    [Fact]
    public void Caps_Comments_At_Maximum_Limit()
    {
        var findings = Enumerable.Range(1, 10)
            .Select(i => new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, $"Issue {i}", "detail", $"src/A.cs:{i}", Array.Empty<string>()))
            .ToList();

        var comments = BatchedReviewBuilder.BuildComments(findings, maxComments: 5);

        Assert.Equal(5, comments.Count);
    }

    [Fact]
    public void Deduplicates_Against_Previous_Metadata_Reviewed_Findings()
    {
        var f1 = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "Old Issue", "detail", "src/A.cs:1", Array.Empty<string>());
        var f2 = new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "New Issue", "detail", "src/A.cs:2", Array.Empty<string>());

        var oldHash = FindingFingerprint.Compute(f1);
        var previousMetadata = new PullRequestMetadata("old_sha", new[] { oldHash });

        var filtered = BatchedReviewBuilder.FilterNewFindings(new[] { f1, f2 }, previousMetadata);

        Assert.Single(filtered);
        Assert.Equal("New Issue", filtered[0].Title);
    }
}
