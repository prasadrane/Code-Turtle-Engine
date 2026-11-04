using System.Net;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Web.Tests;

public class ReviewStreamTests
{
    [Fact]
    public async Task Competing_Connections_Cannot_Consume_Events_And_Reconnect_Recovers_Result()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "stream-fixture-key" })));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "stream-fixture-key");
        var store = app.Services.GetRequiredService<IReviewJobStore>();
        var id = store.CreateJob("https://github.com/o/r/pull/1");
        var writer = store.GetWriter(id);
        await writer.WriteAsync(SseEvent.Phase("validating", "Starting"));
        var url = $"/api/review/{id}/stream";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var first = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.GetAsync(url, timeout.Token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var verdict = new CouncilVerdict([], [], []);
        store.SetResult(id, new ReviewResult("Complete report", verdict, "", new RoslynPayload("r", "HEAD", [])));
        await writer.WriteAsync(SseEvent.Completed("Complete report", verdict), timeout.Token);
        writer.TryComplete();
        var body = await first.Content.ReadAsStringAsync(timeout.Token);
        Assert.Contains("event: phase", body);
        Assert.Contains("event: completed", body);
        Assert.Contains("Complete report", body);

        using var reconnected = await client.GetAsync(url, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, reconnected.StatusCode);
        var recovered = await reconnected.Content.ReadAsStringAsync(timeout.Token);
        Assert.Contains("event: completed", recovered);
        Assert.Contains("Complete report", recovered);
    }

    [Fact]
    public async Task Reconnect_To_Failed_Job_Receives_Safe_Terminal_Error()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "stream-fixture-key" })));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "stream-fixture-key");
        var store = app.Services.GetRequiredService<IReviewJobStore>();
        var id = store.CreateJob("https://github.com/o/r/pull/1");
        store.SetError(id, "raw internal diagnostic must stay private");
        store.GetWriter(id).TryComplete();
        var body = await client.GetStringAsync($"/api/review/{id}/stream");
        Assert.Contains("event: error", body);
        Assert.DoesNotContain("raw internal", body);
    }
}
