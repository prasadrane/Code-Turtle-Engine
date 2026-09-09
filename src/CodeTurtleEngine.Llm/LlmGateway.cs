using Microsoft.Extensions.AI;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace CodeTurtleEngine.Llm;

public interface ILlmGateway
{
    Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default);
}

public sealed class LlmGateway : ILlmGateway
{
    private readonly LlmOptions _options;
    private readonly IChatClientFactory _factory;
    private readonly ResiliencePipeline _pipeline;

    public LlmGateway(LlmOptions options, IChatClientFactory factory, ResiliencePipeline? pipeline = null)
    {
        _options = options;
        _factory = factory;
        _pipeline = pipeline ?? DefaultPipeline();
    }

    public async Task<string> CompleteAsync(string role, IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken ct = default)
    {
        if (!_options.ModelRoles.TryGetValue(role, out var model))
            throw new ModelException($"No model mapped for role '{role}'.");

        var attemptLog = new List<string>();
        Exception? last = null;

        foreach (var route in _options.Routes)
        {
            try
            {
                var client = _factory.Create(route, model);
                var resp = await _pipeline.ExecuteAsync(
                    async token => await client.GetResponseAsync(messages, options, token), ct);
                return resp.Text;
            }
            catch (Exception ex)
            {
                last = ex;
                attemptLog.Add($"{route.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        throw new ProviderException($"All routes failed for role '{role}'.", attemptLog, last);
    }

    private static ResiliencePipeline DefaultPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .AddTimeout(TimeSpan.FromSeconds(60))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
}
