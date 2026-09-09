using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Council.Tests;

public class PersonaRunnerTests
{
    private static RoslynPayload Payload() => new("R", "HEAD", new[]
    {
        new FileFacts("DataAccess.cs", new[]
        {
            new MethodFacts("BuildQuery", Array.Empty<string>(), null, Array.Empty<string>(), new[] { "SampleRepo.DataAccess" })
        })
    });

    [Fact]
    public async Task Runs_Three_Personas_And_Maps_Roles()
    {
        var chat = new FakeStructuredChat((role, _) => new VerdictDto(new[]
        {
            new FindingDto(Severity.Warning, "Issue-" + role, "detail", "File.cs:1", new[] { "SampleRepo.DataAccess" })
        }));
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunAsync(Payload(), "rubric");

        Assert.Equal(3, verdicts.Count);
        Assert.Contains(verdicts, v => v.Persona == PersonaRole.SecurityAuditor);
        Assert.Contains("fast", chat.RolesCalled);
        Assert.Equal(2, chat.RolesCalled.Count(r => r == "deep"));
        Assert.All(verdicts, v => Assert.Single(v.Findings));
    }

    [Fact]
    public async Task Proceeds_At_Quorum_When_One_Persona_Fails()
    {
        var call = 0;
        var chat = new FakeStructuredChat((_, _) =>
        {
            if (Interlocked.Increment(ref call) == 1) throw new InvalidOperationException("boom");
            return new VerdictDto(Array.Empty<FindingDto>());
        });
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        var verdicts = await runner.RunAsync(Payload(), "rubric");

        Assert.Equal(2, verdicts.Count);
    }

    [Fact]
    public async Task Propagates_Cancellation_Instead_Of_Null_Quorum()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var chat = new FakeStructuredChat((_, _) => throw new OperationCanceledException());
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync(Payload(), "rubric", cts.Token));
    }

    [Fact]
    public async Task Throws_ProviderException_Below_Quorum()
    {
        var chat = new FakeStructuredChat((_, _) => throw new InvalidOperationException("down"));
        var runner = new PersonaRunner(chat, new CouncilOptions { Quorum = 2 });

        await Assert.ThrowsAsync<ProviderException>(() => runner.RunAsync(Payload(), "rubric"));
    }
}
