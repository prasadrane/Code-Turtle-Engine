using System.Net;
using System.Net.Http.Json;
using CodeTurtleEngine.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Web.Tests;

public class HostedControlsTests
{
    private sealed class Recorder : IWebReviewPipeline
    {
        public int Started;
        public Task ExecuteAsync(Guid id, CancellationToken ct = default) { Interlocked.Increment(ref Started); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Default_Hosted_Surface_Cannot_Start_Reviews()
    {
        await using var app = new WebApplicationFactory<Program>();
        var response = await app.CreateClient().PostAsJsonAsync("/api/review", new { prUrl = "https://github.com/o/r/pull/1" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Auth_Origin_Allowlist_And_Rate_Control_Admission()
    {
        var recorder = new Recorder();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "fixture-canary-key",
                ["Access:TrustedRepositories:0"] = "o/r", ["Access:Origins:0"] = "http://localhost:3000",
                ["Access:RequestsPerMinute"] = "1" }));
            builder.ConfigureServices(services => services.AddSingleton<IWebReviewPipeline>(recorder));
        });
        using var client = app.CreateClient();
        const string url = "https://github.com/o/r/pull/1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/review", new { prUrl = url })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "fixture-canary-key");
        client.DefaultRequestHeaders.Add("Origin", "https://disallowed.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/review", new { prUrl = url })).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/review", new { prUrl = url })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/review", new { prUrl = url })).StatusCode);
        Assert.InRange(recorder.Started, 0, 1);
    }

    [Fact]
    public async Task Session_Requires_Bearer_And_Cookie_Allows_Authenticated_Sse()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Access:Mode"] = "TrustedLocal", ["Access:ApiKey"] = "fixture-session-key" })));
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/session", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "fixture-session-key");
        var session = await client.PostAsync("/api/session", null);
        Assert.Equal(HttpStatusCode.NoContent, session.StatusCode);
        var cookie = Assert.Single(session.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.DoesNotContain("fixture-session-key", cookie);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/review/{Guid.NewGuid()}/stream")).StatusCode);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Retention_Expires_Cancels_And_Reclaims_Capacity()
    {
        var clock = new Clock();
        var store = new ReviewJobStore(clock: clock, retention: TimeSpan.FromMinutes(1), maxJobs: 1);
        var id = store.CreateJob("fixture");
        Assert.True(store.TryGetJob(id, out var context));
        var token = context!.Cancellation.Token;
        Assert.Throws<InvalidOperationException>(() => store.CreateJob("over-capacity"));
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.False(store.TryGetJob(id, out _));
        Assert.True(token.IsCancellationRequested);
        Assert.True(context.Channel.Reader.Completion.IsCompleted);
        Assert.NotEqual(id, store.CreateJob("next"));
    }

    [Fact]
    public async Task Queue_Is_Bounded_And_Cancellation_Releases_Running_Job()
    {
        var store = new ReviewJobStore();
        var pipeline = new CancellingPipeline();
        using var scheduler = new ReviewScheduler(store, pipeline);
        var first = store.CreateJob("fixture");
        Assert.True(scheduler.TryQueue(first));
        for (var i = 0; i < 7; i++) Assert.True(scheduler.TryQueue(store.CreateJob("fixture")));
        Assert.False(scheduler.TryQueue(store.CreateJob("full")));
        await scheduler.StartAsync(CancellationToken.None);
        await pipeline.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(store.TryGetJob(first, out var job));
        job!.Cancellation.Cancel();
        await job.Channel.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(store.TryGetError(first, out _));
        await scheduler.StopAsync(CancellationToken.None);
    }

    private sealed class CancellingPipeline : IWebReviewPipeline
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
        { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
    }
}
