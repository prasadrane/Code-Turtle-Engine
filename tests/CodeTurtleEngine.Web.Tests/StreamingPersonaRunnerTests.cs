using System.Collections.Concurrent;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using CodeTurtleEngine.Web.Services;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Web.Tests;

public class StreamingPersonaRunnerTests
{
    private static RoslynPayload TestPayload() => new("TestRepo", "commit1", new[]
    {
        new FileFacts("Service.cs", new[]
        {
            new MethodFacts("Process", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "TestRepo.Service" })
        })
    });

    [Fact]
    public async Task Runs_All_Personas_Invokes_OnStarted_And_OnCompleted_Callbacks()
    {
        var startedRoles = new ConcurrentBag<PersonaRole>();
        var completedVerdicts = new ConcurrentBag<PersonaVerdict>();
        var failedRoles = new ConcurrentBag<PersonaRole>();

        var chat = new FakeStructuredChat((role, msgs) => new VerdictDto(new[]
        {
            new FindingDto(Severity.Warning, $"Issue-{role}", "detail", "Service.cs:1", new[] { "TestRepo.Service" })
        }));

        var runner = new StreamingPersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunWithCallbacksAsync(
            TestPayload(),
            "standard rubric",
            role => { startedRoles.Add(role); return Task.CompletedTask; },
            verdict => { completedVerdicts.Add(verdict); return Task.CompletedTask; },
            (role, err) => { failedRoles.Add(role); return Task.CompletedTask; });

        Assert.Equal(3, verdicts.Count);
        Assert.Equal(3, startedRoles.Count);
        Assert.Contains(PersonaRole.AllocationsPerformance, startedRoles);
        Assert.Contains(PersonaRole.SecurityAuditor, startedRoles);
        Assert.Contains(PersonaRole.IdiomaticArchitect, startedRoles);

        Assert.Equal(3, completedVerdicts.Count);
        Assert.Empty(failedRoles);

        Assert.Contains("fast", chat.RolesCalled);
        Assert.Equal(2, chat.RolesCalled.Count(r => r == "deep"));
    }

    [Fact]
    public async Task Invokes_OnFailed_When_One_Persona_Fails_And_Returns_Remaining_Verdicts()
    {
        var startedRoles = new ConcurrentBag<PersonaRole>();
        var completedVerdicts = new ConcurrentBag<PersonaVerdict>();
        var failedRoles = new ConcurrentBag<(PersonaRole Role, string Error)>();

        var chat = new FakeStructuredChat((role, msgs) =>
        {
            if (msgs.Any(m => m.Text?.Contains("security auditor", StringComparison.OrdinalIgnoreCase) == true))
            {
                throw new InvalidOperationException("Security model timeout");
            }
            return new VerdictDto(Array.Empty<FindingDto>());
        });

        var runner = new StreamingPersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunWithCallbacksAsync(
            TestPayload(),
            "rubric",
            role => { startedRoles.Add(role); return Task.CompletedTask; },
            verdict => { completedVerdicts.Add(verdict); return Task.CompletedTask; },
            (role, err) => { failedRoles.Add((role, err)); return Task.CompletedTask; });

        Assert.Equal(2, verdicts.Count);
        Assert.Equal(3, startedRoles.Count);
        Assert.Equal(2, completedVerdicts.Count);
        Assert.Single(failedRoles);

        var failed = failedRoles.First();
        Assert.Equal(PersonaRole.SecurityAuditor, failed.Role);
        Assert.Contains("Security model timeout", failed.Error);
    }

    [Fact]
    public async Task Throws_ProviderException_When_Successes_Fall_Below_Quorum()
    {
        var failedRoles = new ConcurrentBag<PersonaRole>();

        var chat = new FakeStructuredChat((role, msgs) =>
        {
            if (msgs.Any(m => m.Text?.Contains("security", StringComparison.OrdinalIgnoreCase) == true ||
                              m.Text?.Contains("architect", StringComparison.OrdinalIgnoreCase) == true))
            {
                throw new InvalidOperationException("Model overload");
            }
            return new VerdictDto(Array.Empty<FindingDto>());
        });

        var runner = new StreamingPersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var ex = await Assert.ThrowsAsync<ProviderException>(() => runner.RunWithCallbacksAsync(
            TestPayload(),
            "rubric",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (role, _) => { failedRoles.Add(role); return Task.CompletedTask; }));

        Assert.Contains("Council quorum not met", ex.Message);
        Assert.Equal(2, failedRoles.Count);
    }

    [Fact]
    public async Task Propagates_OperationCanceledException_When_Cancellation_Requested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var chat = new FakeStructuredChat((_, _) => throw new OperationCanceledException());
        var runner = new StreamingPersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunWithCallbacksAsync(
            TestPayload(),
            "rubric",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            cts.Token));
    }

    [Fact]
    public async Task Runs_Personas_Concurrently()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeCount = 0;
        var maxConcurrent = 0;

        var chat = new FakeAsyncStructuredChat(async (role, msgs, ct) =>
        {
            var count = Interlocked.Increment(ref activeCount);
            lock (tcs)
            {
                if (count > maxConcurrent) maxConcurrent = count;
            }
            if (count == 3)
            {
                tcs.TrySetResult(true);
            }
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            Interlocked.Decrement(ref activeCount);
            return new VerdictDto(Array.Empty<FindingDto>());
        });

        var runner = new StreamingPersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunWithCallbacksAsync(
            TestPayload(),
            "rubric",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        Assert.Equal(3, verdicts.Count);
        Assert.Equal(3, maxConcurrent);
    }

    private sealed class FakeStructuredChat : IStructuredChatClient
    {
        private readonly Func<string, IReadOnlyList<ChatMessage>, object> _responder;
        public List<string> RolesCalled { get; } = new();

        public FakeStructuredChat(Func<string, IReadOnlyList<ChatMessage>, object> responder)
            => _responder = responder;

        public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
            string jsonSchema, CancellationToken ct = default)
        {
            lock (RolesCalled) RolesCalled.Add(role);
            return Task.FromResult((T)_responder(role, messages));
        }
    }

    private sealed class FakeAsyncStructuredChat : IStructuredChatClient
    {
        private readonly Func<string, IReadOnlyList<ChatMessage>, CancellationToken, Task<object>> _responder;

        public FakeAsyncStructuredChat(Func<string, IReadOnlyList<ChatMessage>, CancellationToken, Task<object>> responder)
            => _responder = responder;

        public async Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
            string jsonSchema, CancellationToken ct = default)
        {
            var res = await _responder(role, messages, ct);
            return (T)res;
        }
    }
}
