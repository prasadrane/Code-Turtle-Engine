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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Singletons & Services
builder.Services.AddSingleton(llmOptions);
builder.Services.AddSingleton(turtleOptions);
builder.Services.AddSingleton(turtleOptions.Council);

builder.Services.AddSingleton<IReviewJobStore, ReviewJobStore>();
builder.Services.AddHttpClient<IGitHubPrFetcher, GitHubPrFetcher>();
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

var app = builder.Build();

app.UseCors();

// 1. GET /health
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// 2. POST /api/review
app.MapPost("/api/review", (GitHubPrRequest? request, IReviewJobStore jobStore, IWebReviewPipeline pipeline) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.PrUrl) || !PrUrlParser.TryParse(request.PrUrl, out _))
    {
        return Results.BadRequest(new { error = "Invalid GitHub PR URL. Expected format: https://github.com/owner/repo/pull/123" });
    }

    var jobId = jobStore.CreateJob(request.PrUrl);

    _ = Task.Run(async () =>
    {
        try
        {
            await pipeline.ExecuteAsync(jobId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            jobStore.SetError(jobId, ex.Message);
            try
            {
                var writer = jobStore.GetWriter(jobId);
                await writer.WriteAsync(SseEvent.Error("INTERNAL_ERROR", ex.Message));
                writer.TryComplete();
            }
            catch { }
        }
    });

    return Results.Accepted($"/api/review/{jobId}/result", new
    {
        jobId = jobId.ToString(),
        estimatedDurationSeconds = 180
    });
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
            artifactDir = result.ArtifactDir
        });
    }

    if (jobStore.TryGetError(jobId, out var error) && error is not null)
    {
        return Results.Ok(new
        {
            status = "failed",
            error = error
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
