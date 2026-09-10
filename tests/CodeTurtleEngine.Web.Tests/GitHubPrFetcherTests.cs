using System.Net;
using CodeTurtleEngine.Web.Services;

namespace CodeTurtleEngine.Web.Tests;

public class GitHubPrFetcherTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task FetchAsync_WhenPrNotFound_ReturnsIsAccessibleFalse()
    {
        var handler = new StubHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var fetcher = new GitHubPrFetcher(client);

        var result = await fetcher.FetchAsync("owner", "repo", 42);

        Assert.False(result.IsAccessible);
        Assert.False(result.IsDotNet);
        Assert.Empty(result.ChangedFiles);
    }

    [Fact]
    public async Task FetchAsync_WhenForbidden_ReturnsIsAccessibleFalse()
    {
        var handler = new StubHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = new HttpClient(handler);
        var fetcher = new GitHubPrFetcher(client);

        var result = await fetcher.FetchAsync("owner", "repo", 42);

        Assert.False(result.IsAccessible);
    }

    [Fact]
    public async Task FetchAsync_WhenDotNetPr_ReturnsIsDotNetTrueAndExtractedDetails()
    {
        var prJson = """
        {
            "base": { "sha": "base123" },
            "head": {
                "sha": "head456",
                "ref": "feature-turtle",
                "repo": { "clone_url": "https://github.com/contributor/repo.git" }
            }
        }
        """;

        var filesJson = """
        [
            { "filename": "src/Core/Engine.cs", "status": "modified" },
            { "filename": "README.md", "status": "modified" }
        ]
        """;

        var handler = new StubHttpMessageHandler(req =>
        {
            Assert.Contains("CodeTurtleEngine-Web", req.Headers.UserAgent.ToString());
            if (req.RequestUri!.ToString().Contains("/files"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(filesJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(prJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var fetcher = new GitHubPrFetcher(client);

        var result = await fetcher.FetchAsync("owner", "repo", 100);

        Assert.True(result.IsAccessible);
        Assert.True(result.IsDotNet);
        Assert.Equal("base123", result.BaseSha);
        Assert.Equal("head456", result.HeadSha);
        Assert.Equal("feature-turtle", result.HeadBranch);
        Assert.Equal("https://github.com/contributor/repo.git", result.CloneUrl);
        Assert.Equal(2, result.ChangedFiles.Count);
        Assert.Contains("src/Core/Engine.cs", result.ChangedFiles);
        Assert.Empty(result.LanguageMessage);
    }

    [Fact]
    public async Task FetchAsync_WhenPythonPr_ReturnsIsDotNetFalseWithFunnyMessage()
    {
        var prJson = """
        {
            "base": { "sha": "base000" },
            "head": {
                "sha": "head111",
                "ref": "patch-py",
                "repo": { "clone_url": "https://github.com/owner/repo.git" }
            }
        }
        """;

        var filesJson = """
        [
            { "filename": "app/main.py", "status": "added" }
        ]
        """;

        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("/files"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(filesJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(prJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var fetcher = new GitHubPrFetcher(client);

        var result = await fetcher.FetchAsync("owner", "repo", 7);

        Assert.True(result.IsAccessible);
        Assert.False(result.IsDotNet);
        Assert.Single(result.ChangedFiles);
        Assert.Contains("curly braces", result.LanguageMessage);
    }
}
