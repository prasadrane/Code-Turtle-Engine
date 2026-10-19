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
    private ReviewChanges? _changes;
    public string? AccessOwner { get; set; }
    public ReviewChanges? Changes
    {
        get { lock (_sync) return _changes; }
        set { lock (_sync) _changes = value; }
    }

    public CancellationTokenSource Cancellation { get; } = new();
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
    private readonly object _admission = new();
    private readonly TimeProvider _clock;
    private readonly TimeSpan _retention;
    private readonly int _maxJobs;

    public ReviewJobStore(int? channelCapacity = 256, TimeProvider? clock = null, TimeSpan? retention = null, int maxJobs = 128)
    {
        if (channelCapacity.HasValue && channelCapacity.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCapacity), "Channel capacity must be greater than zero.");
        }
        _channelCapacity = channelCapacity;
        _clock = clock ?? TimeProvider.System;
        _retention = retention ?? TimeSpan.FromMinutes(30);
        _maxJobs = maxJobs;
        if (_retention <= TimeSpan.Zero || maxJobs < 1) throw new ArgumentOutOfRangeException(nameof(retention));
    }

    public Guid CreateJob(string prUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prUrl);
        lock (_admission)
        {
        Expire();
        if (_jobs.Count >= _maxJobs) throw new InvalidOperationException("Review retention capacity exceeded.");
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

        var context = new ReviewJobContext(jobId, prUrl, channel, _clock.GetUtcNow());
        _jobs[jobId] = context;
        return jobId;
        }
    }

    public void Expire()
    {
        foreach (var (id, job) in _jobs)
            if (_clock.GetUtcNow() - job.CreatedAt >= _retention && _jobs.TryRemove(id, out _))
            {
                job.Cancellation.Cancel();
                job.Channel.Writer.TryComplete();
            }
    }

    public bool TryGetJob(Guid jobId, out ReviewJobContext? context)
    {
        Expire();
        return _jobs.TryGetValue(jobId, out context);
    }

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
