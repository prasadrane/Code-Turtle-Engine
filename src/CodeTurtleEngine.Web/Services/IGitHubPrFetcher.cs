using CodeTurtleEngine.Web.Models;

namespace CodeTurtleEngine.Web.Services;

public record GitHubPrDetails(
    bool IsAccessible,
    bool IsDotNet,
    string BaseSha,
    string HeadSha,
    string HeadBranch,
    IReadOnlyList<string> ChangedFiles,
    string LanguageMessage,
    string CloneUrl = "");

public interface IGitHubPrFetcher
{
    Task<GitHubPrDetails> FetchAsync(string owner, string repo, int pullNumber, CancellationToken ct = default);

    Task<GitHubPrDetails> FetchAsync(ParsedPrUrl parsedUrl, CancellationToken ct = default) =>
        FetchAsync(parsedUrl.Owner, parsedUrl.Repo, parsedUrl.PullNumber, ct);
}
