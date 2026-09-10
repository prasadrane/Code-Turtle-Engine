using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Web.Models;

namespace CodeTurtleEngine.Web.Services;

public sealed class ReviewJobContext
{
    private readonly object _sync = new();
    private ReviewResult? _result;
    private string? _error;

    public Guid JobId { get; }
    public string PrUrl { get; }
    public DateTimeOffset CreatedAt { get; }
    public Channel<SseEvent> Channel { get; }

    public ReviewJobContext(Guid jobId, string prUrl, Channel<SseEvent> channel, DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prUrl);
        JobId = jobId;
        PrUrl = prUrl;
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public ReviewResult? Result
    {
        get { lock (_sync) return _result; }
        set { lock (_sync) _result = value; }
    }

    public string? Error
    {
        get { lock (_sync) return _error; }
        set { lock (_sync) _error = value; }
    }

    public bool IsCompleted
    {
        get { lock (_sync) return _result is not null || _error is not null; }
    }
}

public interface IReviewJobStore
{
    Guid CreateJob(string prUrl);
    bool TryGetJob(Guid jobId, out ReviewJobContext? context);
    ChannelWriter<SseEvent> GetWriter(Guid jobId);
    ChannelReader<SseEvent> GetReader(Guid jobId);
    void SetResult(Guid jobId, ReviewResult result);
    bool TryGetResult(Guid jobId, out ReviewResult? result);
    void SetError(Guid jobId, string error);
    bool TryGetError(Guid jobId, out string? error);
}

public sealed class ReviewJobStore : IReviewJobStore
{
    private readonly ConcurrentDictionary<Guid, ReviewJobContext> _jobs = new();
    private readonly int? _channelCapacity;

    public ReviewJobStore(int? channelCapacity = null)
    {
        if (channelCapacity.HasValue && channelCapacity.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCapacity), "Channel capacity must be greater than zero.");
        }
        _channelCapacity = channelCapacity;
    }

    public Guid CreateJob(string prUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prUrl);
        var jobId = Guid.NewGuid();
        var channel = _channelCapacity.HasValue
            ? Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(_channelCapacity.Value)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            })
            : Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        var context = new ReviewJobContext(jobId, prUrl, channel);
        _jobs[jobId] = context;
        return jobId;
    }

    public bool TryGetJob(Guid jobId, out ReviewJobContext? context) =>
        _jobs.TryGetValue(jobId, out context);

    public ChannelWriter<SseEvent> GetWriter(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var context))
        {
            throw new KeyNotFoundException($"Job '{jobId}' was not found.");
        }
        return context.Channel.Writer;
    }

    public ChannelReader<SseEvent> GetReader(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var context))
        {
            throw new KeyNotFoundException($"Job '{jobId}' was not found.");
        }
        return context.Channel.Reader;
    }

    public void SetResult(Guid jobId, ReviewResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!_jobs.TryGetValue(jobId, out var context))
        {
            throw new KeyNotFoundException($"Job '{jobId}' was not found.");
        }
        context.Result = result;
    }

    public bool TryGetResult(Guid jobId, out ReviewResult? result)
    {
        if (_jobs.TryGetValue(jobId, out var context) && context.Result is not null)
        {
            result = context.Result;
            return true;
        }
        result = null;
        return false;
    }

    public void SetError(Guid jobId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        if (!_jobs.TryGetValue(jobId, out var context))
        {
            throw new KeyNotFoundException($"Job '{jobId}' was not found.");
        }
        context.Error = error;
    }

    public bool TryGetError(Guid jobId, out string? error)
    {
        if (_jobs.TryGetValue(jobId, out var context) && context.Error is not null)
        {
            error = context.Error;
            return true;
        }
        error = null;
        return false;
    }
}
