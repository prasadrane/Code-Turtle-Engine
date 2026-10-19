using System.Net;
using System.Net.Http.Json;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Web.Tests;

public sealed class ChangesEndpointTests
{
    [Fact]
    public async Task SourceIsSessionBound_NoStore_AndExpiresWithJob()
    {
        var clock = new Clock();
        var store = new ReviewJobStore(clock: clock, retention: TimeSpan.FromMinutes(1));
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "test-key",
                ["Access:TrustedRepositories:0"] = "fixture/repo", ["Access:RequestsPerMinute"] = "1000" }));
            builder.ConfigureServices(services => {
                services.AddSingleton<IReviewJobStore>(store);
                services.AddSingleton<IWebReviewPipeline, NoWork>();
            });
        });
        using var owner = app.CreateClient();
        using var other = app.CreateClient();
        foreach (var client in new[] { owner, other })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", "test-key");
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/session", null)).StatusCode);
            client.DefaultRequestHeaders.Authorization = null;
        }
        var start = await owner.PostAsJsonAsync("/api/review", new { prUrl = "https://github.com/fixture/repo/pull/1" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var json = await start.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = json.GetProperty("jobId").GetGuid();
        var uri = $"/api/review/{id}/changes";
        Assert.Equal(HttpStatusCode.Accepted, (await owner.GetAsync(uri)).StatusCode);
        store.TryGetJob(id, out var job);
        job!.Changes = new ReviewChanges(1, new("fixture/repo", 1, "Title", "a", "b", "a", "merge-base-to-head"),
            [new("Service.cs", "Service.cs", "Modified", true, "SOURCE_CANARY", [])], []);
        var result = await owner.GetAsync(uri);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        Assert.Contains("SOURCE_CANARY", await result.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/review/{id}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/review/{id}/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/review/{id}")).StatusCode);
        Assert.False(job.Cancellation.IsCancellationRequested);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(uri)).StatusCode);
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/review/{id}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/review/{id}/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/review/{id}")).StatusCode);
        Assert.True(job.Cancellation.IsCancellationRequested);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class NoWork : IWebReviewPipeline
    {
        public Task ExecuteAsync(Guid jobId, CancellationToken ct = default) => Task.Delay(Timeout.Infinite, ct);
    }
}
