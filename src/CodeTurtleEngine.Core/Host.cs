using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodeTurtleEngine.Core;

public sealed record PullRequestComment(
    string Path,
    int Line,
    string Body,
    string? Side = "RIGHT");

public sealed record PullRequestMetadata(
    string LastReviewedSha,
    IReadOnlyList<string> ReviewedFindingHashes);

public sealed record BatchedReviewRequest(
    string CommitSha,
    string SummaryMarkdown,
    IReadOnlyList<PullRequestComment> Comments,
    PullRequestMetadata Metadata);

public interface IPullRequestHost
{
    Task PostReviewAsync(string repoSlug, string prNumber, BatchedReviewRequest request, CancellationToken ct = default);
}

public static class FindingFingerprint
{
    public static string Compute(ReviewFinding finding)
    {
        var raw = $"{finding.Location}|{finding.Title}|{string.Join(",", finding.CitedSymbolFqns)}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).Substring(0, 16).ToLowerInvariant();
    }
}

public static class PullRequestMarker
{
    public const string MarkerPrefix = "<!-- code-turtle-metadata:";
    public const string MarkerSuffix = "-->";

    public static string Format(PullRequestMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata);
        return $"{MarkerPrefix} {json} {MarkerSuffix}";
    }

    public static PullRequestMetadata? Parse(string text)
    {
        var start = text.IndexOf(MarkerPrefix, StringComparison.Ordinal);
        if (start < 0) return null;
        var end = text.IndexOf(MarkerSuffix, start, StringComparison.Ordinal);
        if (end < 0) return null;
        var json = text.Substring(start + MarkerPrefix.Length, end - (start + MarkerPrefix.Length)).Trim();
        try
        {
            return JsonSerializer.Deserialize<PullRequestMetadata>(json);
        }
        catch
        {
            return null;
        }
    }
}

public static class BatchedReviewBuilder
{
    public static IReadOnlyList<ReviewFinding> FilterNewFindings(
        IEnumerable<ReviewFinding> findings,
        PullRequestMetadata? previousMetadata)
    {
        if (previousMetadata is null || previousMetadata.ReviewedFindingHashes.Count == 0)
            return findings.ToList();

        var set = new HashSet<string>(previousMetadata.ReviewedFindingHashes, StringComparer.OrdinalIgnoreCase);
        return findings.Where(f => !set.Contains(FindingFingerprint.Compute(f))).ToList();
    }

    public static IReadOnlyList<PullRequestComment> BuildComments(
        IEnumerable<ReviewFinding> findings,
        int maxComments = 5)
    {
        var comments = new List<PullRequestComment>();
        foreach (var f in findings.Take(maxComments))
        {
            var (path, line) = ParseLocation(f.Location);
            var body = $"### [{f.Severity}] {f.Title}\n\n{f.Detail}";
            if (!string.IsNullOrEmpty(f.SuggestedCode))
            {
                body += $"\n\n```suggestion\n{f.SuggestedCode}\n```";
            }
            if (f.CitedSymbolFqns.Count > 0)
            {
                body += $"\n\n> Verified citations: {string.Join(", ", f.CitedSymbolFqns)}";
            }
            comments.Add(new PullRequestComment(path, Math.Max(1, line), body));
        }
        return comments;
    }

    private static (string path, int line) ParseLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return ("unknown", 1);
        var colon = location.LastIndexOf(':');
        if (colon < 0) return (location, 1);
        var path = location.Substring(0, colon);
        var linePart = location.Substring(colon + 1);
        var dash = linePart.IndexOf('-');
        if (dash > 0) linePart = linePart.Substring(0, dash);
        int.TryParse(linePart, out var line);
        return (path, Math.Max(1, line));
    }
}
