using System.Threading.Channels;

namespace CodeTurtleEngine.Web.Services;

public sealed class ReviewScheduler(IReviewJobStore store, IWebReviewPipeline pipeline) : BackgroundService
{
    private readonly Channel<Guid> _queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(8) {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = false, SingleWriter = false });

    public bool TryQueue(Guid id) => _queue.Writer.TryWrite(id);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Consume(stoppingToken)));

    private async Task Consume(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                if (!store.TryGetJob(id, out var context) || context is null) continue;
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, context.Cancellation.Token);
                lifetime.CancelAfter(TimeSpan.FromMinutes(2));
                try { await pipeline.ExecuteAsync(id, lifetime.Token); }
                catch (Exception) { /* No source-controlled exception messages in logs or clients. */ }
                finally
                {
                    if (store.TryGetJob(id, out var current) && current is not null)
                    {
                        if (!current.IsCompleted) store.SetError(id, "Review cancelled, timed out, or failed.");
                        current.Channel.Writer.TryComplete();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

public sealed class ReviewRetention(IReviewJobStore store) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                if (store is ReviewJobStore bounded) bounded.Expire();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
