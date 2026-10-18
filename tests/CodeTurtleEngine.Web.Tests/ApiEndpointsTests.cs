using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Web.Tests;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "fixture-key",
                ["Access:TrustedRepositories:0"] = "dotnet/runtime" }));
            builder.ConfigureServices(services => services.AddSingleton<CodeTurtleEngine.Web.Services.IWebReviewPipeline, NoExternalPipeline>());
        });
    }

    [Fact]
    public async Task Health_Endpoint_Returns200AndHealthyStatus()
    {
        var client = Client();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://google.com")]
    public async Task PostReview_WithInvalidUrl_Returns400BadRequest(string invalidUrl)
    {
        var client = Client();
        var request = new GitHubPrRequest(invalidUrl);

        var response = await client.PostAsJsonAsync("/api/review", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReview_WithValidUrl_Returns202AcceptedWithJobIdAndEstimatedDuration()
    {
        var client = Client();
        var request = new GitHubPrRequest("https://github.com/dotnet/runtime/pull/101");

        var response = await client.PostAsJsonAsync("/api/review", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("jobId", out var jobIdProp));
        Assert.True(Guid.TryParse(jobIdProp.GetString(), out _));
        Assert.True(root.TryGetProperty("estimatedDurationSeconds", out var durationProp));
        Assert.Equal(180, durationProp.GetInt32());
    }

    [Fact]
    public async Task GetResult_WithNonExistentJobId_Returns404NotFound()
    {
        var client = Client();
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/review/{nonExistentId}/result");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetResult_ForExistingJob_ReturnsRunningStatusInitially()
    {
        var client = Client();
        var store = _factory.Services.GetRequiredService<IReviewJobStore>();
        var jobId = store.CreateJob("https://github.com/dotnet/runtime/pull/101");

        var response = await client.GetAsync($"/api/review/{jobId}/result");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("running", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetResult_ForCompletedJob_ReturnsCompletedStatusAndResult()
    {
        var client = Client();
        var store = _factory.Services.GetRequiredService<IReviewJobStore>();
        var jobId = store.CreateJob("https://github.com/dotnet/runtime/pull/102");

        var verdict = new CouncilVerdict(
            Array.Empty<PersonaVerdict>(),
            Array.Empty<ReviewFinding>(),
            Array.Empty<GuardResult>());
        var reviewResult = new ReviewResult("# Review Output", verdict, "/tmp/artifacts", new RoslynPayload("r", "HEAD", Array.Empty<FileFacts>()));
        store.SetResult(jobId, reviewResult);

        var response = await client.GetAsync($"/api/review/{jobId}/result");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("# Review Output", root.GetProperty("markdown").GetString());
    }

    [Fact]
    public async Task GetStream_WithNonExistentJobId_Returns404NotFound()
    {
        var client = Client();
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/review/{nonExistentId}/stream");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStream_ForExistingJob_StreamsEventsWithEventStreamContentType()
    {
        var client = Client();
        var store = _factory.Services.GetRequiredService<IReviewJobStore>();
        var jobId = store.CreateJob("https://github.com/dotnet/runtime/pull/103");

        var writer = store.GetWriter(jobId);
        await writer.WriteAsync(SseEvent.Phase("validating", "Validating PR..."));
        writer.Complete();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/review/{jobId}/stream");
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: phase", content);
        Assert.Contains("validating", content);
    }
    private HttpClient Client()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fixture-key");
        return client;
    }
    private sealed class NoExternalPipeline : CodeTurtleEngine.Web.Services.IWebReviewPipeline
    {
        public Task ExecuteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
