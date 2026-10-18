using System.Text.Json;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Llm;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var llmOptions = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
var turtleOptions = builder.Configuration.GetSection(TurtleOptions.SectionName).Get<TurtleOptions>() ?? new TurtleOptions();
turtleOptions.Council.PromptMode = CodeTurtleEngine.Core.PromptMode.Grounded;

var access = builder.Configuration.GetSection("Access").Get<HostedAccessOptions>() ?? new();
builder.Services.AddReviewAccess(builder.Configuration);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Access:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "DELETE").AllowCredentials();
}));

// Singletons & Services
builder.Services.AddSingleton(llmOptions);
builder.Services.AddSingleton(turtleOptions);
builder.Services.AddSingleton(turtleOptions.Council);

builder.Services.AddSingleton<IReviewJobStore, ReviewJobStore>();
builder.Services.AddHttpClient<IGitHubPrFetcher, GitHubPrFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
});
builder.Services.AddSingleton<IRepoCloner, RepoCloner>();
builder.Services.AddSingleton<ICompilationLoader, MsBuildCompilationLoader>();
builder.Services.AddSingleton<IArbiter, Arbiter>();
builder.Services.AddSingleton<ITurtleShellGuard>(sp => new TurtleShellGuard(sp.GetRequiredService<CouncilOptions>()));
builder.Services.AddSingleton<RubricLoader>();

builder.Services.AddSingleton<IChatClientFactory, BailianChatClientFactory>();
builder.Services.AddSingleton<ILlmGateway>(sp => new LlmGateway(
    sp.GetRequiredService<LlmOptions>(),
    sp.GetRequiredService<IChatClientFactory>()));
builder.Services.AddSingleton<IStructuredChatClient>(sp => new StructuredChatClient(
    sp.GetRequiredService<ILlmGateway>(),
    sp.GetRequiredService<LlmOptions>().JsonRetries));
builder.Services.AddSingleton<IStreamingPersonaRunner, StreamingPersonaRunner>();
builder.Services.AddSingleton<IWebReviewPipeline, WebReviewPipeline>();

builder.Services.AddSingleton<ReviewScheduler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReviewScheduler>());
builder.Services.AddHostedService<ReviewRetention>();
var app = builder.Build();

app.Use(HostedAccess.Enforce);
app.UseCors();
app.UseRateLimiter();

// 1. GET /health
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/api/session", (HttpContext context, ReviewSessions sessions) =>
{
    try
    {
        context.Response.Cookies.Append("turtle-session", sessions.Issue(), new CookieOptions {
            HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(15), Path = "/api" });
        return Results.NoContent();
    }
    catch (InvalidOperationException) { return Results.StatusCode(429); }
}).RequireRateLimiting("review");

// 2. POST /api/review
app.MapPost("/api/review", (GitHubPrRequest? request, IReviewJobStore jobStore, ReviewScheduler scheduler, HostedAccessOptions policy) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.PrUrl) || !PrUrlParser.TryParse(request.PrUrl, out _))
    {
        return Results.BadRequest(new { error = "Invalid GitHub PR URL. Expected format: https://github.com/owner/repo/pull/123" });
    }

    PrUrlParser.TryParse(request.PrUrl, out var parsed);
    if (!policy.TrustedRepositories.Contains($"{parsed!.Owner}/{parsed.Repo}", StringComparer.OrdinalIgnoreCase))
        return Results.StatusCode(403);
    Guid jobId;
    try { jobId = jobStore.CreateJob(request.PrUrl); }
    catch (InvalidOperationException) { return Results.StatusCode(429); }
    if (!scheduler.TryQueue(jobId))
    {
        jobStore.SetError(jobId, "Review queue full.");
        jobStore.GetWriter(jobId).TryComplete();
        return Results.StatusCode(429);
    }

    return Results.Accepted($"/api/review/{jobId}/result", new
    {
        jobId = jobId.ToString(),
        estimatedDurationSeconds = 180
    });
}).RequireRateLimiting("review");

app.MapDelete("/api/review/{jobId:guid}", (Guid jobId, IReviewJobStore store) =>
{
    if (!store.TryGetJob(jobId, out var job) || job is null) return Results.NotFound();
    job.Cancellation.Cancel();
    return Results.Accepted();
});

// 3. GET /api/review/{jobId}/stream
app.MapGet("/api/review/{jobId:guid}/stream", async (Guid jobId, HttpContext httpContext, IReviewJobStore jobStore) =>
{
    if (!jobStore.TryGetJob(jobId, out var jobContext) || jobContext is null)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(new { error = $"Job '{jobId}' not found." });
        return;
    }

    httpContext.Response.Headers.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var reader = jobStore.GetReader(jobId);
    var ct = httpContext.RequestAborted;

    try
    {
        await foreach (var sseEvent in reader.ReadAllAsync(ct))
        {
            var json = JsonSerializer.Serialize(sseEvent.Data);
            await httpContext.Response.WriteAsync($"event: {sseEvent.Type}\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // Client disconnected
    }
});

// 4. GET /api/review/{jobId}/result
app.MapGet("/api/review/{jobId:guid}/result", (Guid jobId, IReviewJobStore jobStore) =>
{
    if (!jobStore.TryGetJob(jobId, out var jobContext) || jobContext is null)
    {
        return Results.NotFound(new { error = $"Job '{jobId}' not found." });
    }

    if (jobStore.TryGetResult(jobId, out var result) && result is not null)
    {
        return Results.Ok(new
        {
            status = "completed",
            markdown = result.Markdown,
            verdict = result.Verdict,
            evidence = "citation-existence-only"
        });
    }

    if (jobStore.TryGetError(jobId, out var error) && error is not null)
    {
        return Results.Ok(new
        {
            status = "failed",
            error = "Review failed, cancelled, or expired."
        });
    }

    return Results.Ok(new
    {
        status = "running",
        jobId = jobId.ToString(),
        elapsedSeconds = (int)(DateTimeOffset.UtcNow - jobContext.CreatedAt).TotalSeconds
    });
});

app.Run();

public partial class Program { }
