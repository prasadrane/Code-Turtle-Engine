using System.Net.Http.Headers;
using System.Text.Json;
using CodeTurtleEngine.Web.Models;

namespace CodeTurtleEngine.Web.Services;

public sealed class GitHubPrFetcher : IGitHubPrFetcher
{
    private const string UserAgent = "CodeTurtleEngine-Web";
    private readonly HttpClient _httpClient;

    public GitHubPrFetcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token) && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<GitHubPrDetails> FetchAsync(string owner, string repo, int pullNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        var prUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}";
        using var prRequest = new HttpRequestMessage(HttpMethod.Get, prUrl);
        prRequest.Headers.UserAgent.ParseAdd(UserAgent);

        HttpResponseMessage prResponse;
        try
        {
            prResponse = await _httpClient.SendAsync(prRequest, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return Inaccessible();
        }

        if (!prResponse.IsSuccessStatusCode)
        {
            return Inaccessible();
        }

        string prJson = await prResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var prDoc = JsonDocument.Parse(prJson);
        var prRoot = prDoc.RootElement;

        var baseSha = prRoot.TryGetProperty("base", out var baseProp) && baseProp.TryGetProperty("sha", out var bSha)
            ? bSha.GetString() ?? string.Empty
            : string.Empty;

        var headProp = prRoot.TryGetProperty("head", out var hp) ? hp : default;
        var headSha = headProp.ValueKind != JsonValueKind.Undefined && headProp.TryGetProperty("sha", out var hSha)
            ? hSha.GetString() ?? string.Empty
            : string.Empty;

        var headBranch = headProp.ValueKind != JsonValueKind.Undefined && headProp.TryGetProperty("ref", out var hRef)
            ? hRef.GetString() ?? string.Empty
            : string.Empty;

        var cloneUrl = headProp.ValueKind != JsonValueKind.Undefined &&
                       headProp.TryGetProperty("repo", out var headRepo) &&
                       headRepo.ValueKind == JsonValueKind.Object &&
                       headRepo.TryGetProperty("clone_url", out var cUrl)
            ? cUrl.GetString() ?? $"https://github.com/{owner}/{repo}.git"
            : $"https://github.com/{owner}/{repo}.git";

        var filesUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}/files?per_page=100";
        using var filesRequest = new HttpRequestMessage(HttpMethod.Get, filesUrl);
        filesRequest.Headers.UserAgent.ParseAdd(UserAgent);

        HttpResponseMessage filesResponse;
        try
        {
            filesResponse = await _httpClient.SendAsync(filesRequest, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return Inaccessible();
        }

        if (!filesResponse.IsSuccessStatusCode)
        {
            return Inaccessible();
        }

        string filesJson = await filesResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var filesDoc = JsonDocument.Parse(filesJson);
        var changedFiles = new List<string>();

        if (filesDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in filesDoc.RootElement.EnumerateArray())
            {
                if (elem.TryGetProperty("filename", out var fn) && fn.GetString() is { } path)
                {
                    changedFiles.Add(path);
                }
            }
        }

        var (isDotNet, _, funny) = LanguageDetector.Inspect(changedFiles);

        return new GitHubPrDetails(
            IsAccessible: true,
            IsDotNet: isDotNet,
            BaseSha: baseSha,
            HeadSha: headSha,
            HeadBranch: headBranch,
            ChangedFiles: changedFiles.AsReadOnly(),
            LanguageMessage: isDotNet ? string.Empty : funny,
            CloneUrl: cloneUrl);

        static GitHubPrDetails Inaccessible() => new(
            IsAccessible: false,
            IsDotNet: false,
            BaseSha: string.Empty,
            HeadSha: string.Empty,
            HeadBranch: string.Empty,
            ChangedFiles: Array.Empty<string>(),
            LanguageMessage: "Repository or pull request is private, invalid, or inaccessible.");
    }
}
