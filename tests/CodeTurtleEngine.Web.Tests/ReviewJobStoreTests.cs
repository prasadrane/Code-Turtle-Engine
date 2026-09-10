using System.Text.Json;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using CodeTurtleEngine.Web.Services;

namespace CodeTurtleEngine.Web.Tests;

public class ReviewJobStoreTests
{
    private static CouncilVerdict CreateTestVerdict()
    {
        var findings = new List<ReviewFinding>
        {
            new(PersonaRole.AllocationsPerformance, Severity.Info, "Fast check", "Looks fast", "File.cs:1", new[] { "App.Run" })
        };
        var personas = new List<PersonaVerdict>
        {
            new(PersonaRole.AllocationsPerformance, findings)
        };
        var audit = new List<GuardResult>
        {
            new("App.Run", true)
        };
        return new CouncilVerdict(personas, findings, audit);
    }

    private static ReviewResult CreateTestResult(string markdown = "# Review") =>
        new(markdown, CreateTestVerdict(), "artifacts/sample");

    [Fact]
    public void CreateJob_GeneratesUniqueNonEmptyGuids()
    {
        var store = new ReviewJobStore();
        const string prUrl = "https://github.com/octocat/Hello-World/pull/42";

        var id1 = store.CreateJob(prUrl);
        var id2 = store.CreateJob(prUrl);

        Assert.NotEqual(Guid.Empty, id1);
        Assert.NotEqual(Guid.Empty, id2);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void CreateJob_NullOrWhitespacePrUrl_ThrowsArgumentException()
    {
        var store = new ReviewJobStore();

        Assert.Throws<ArgumentNullException>(() => store.CreateJob(null!));
        Assert.Throws<ArgumentException>(() => store.CreateJob("   "));
    }

    [Fact]
    public void TryGetJob_ExistingJob_ReturnsTrueAndPopulatedContext()
    {
        var store = new ReviewJobStore();
        const string prUrl = "https://github.com/octocat/Hello-World/pull/1";
        var jobId = store.CreateJob(prUrl);

        var found = store.TryGetJob(jobId, out var context);

        Assert.True(found);
        Assert.NotNull(context);
        Assert.Equal(jobId, context.JobId);
        Assert.Equal(prUrl, context.PrUrl);
        Assert.False(context.IsCompleted);
        Assert.Null(context.Result);
        Assert.Null(context.Error);
    }

    [Fact]
    public void TryGetJob_NonExistentJob_ReturnsFalse()
    {
        var store = new ReviewJobStore();

        var found = store.TryGetJob(Guid.NewGuid(), out var context);

        Assert.False(found);
        Assert.Null(context);
    }

    [Fact]
    public async Task Channel_EmitsAndReceivesEventsInFifoOrder()
    {
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/octocat/Hello-World/pull/10");

        var writer = store.GetWriter(jobId);
        var reader = store.GetReader(jobId);

        var evt1 = SseEvent.Phase("analysis", "Analyzing PR...");
        var evt2 = SseEvent.PersonaStarted("speedy", "Speedy ⚡");
        var evt3 = SseEvent.Completed("# Done", CreateTestVerdict());

        await writer.WriteAsync(evt1);
        await writer.WriteAsync(evt2);
        await writer.WriteAsync(evt3);
        writer.Complete();

        var received = new List<SseEvent>();
        await foreach (var item in reader.ReadAllAsync())
        {
            received.Add(item);
        }

        Assert.Equal(3, received.Count);
        Assert.Equal(SseEventTypes.Phase, received[0].Type);
        Assert.Equal(SseEventTypes.PersonaStarted, received[1].Type);
        Assert.Equal(SseEventTypes.Completed, received[2].Type);
    }

    [Fact]
    public void SetResult_And_TryGetResult_ThreadSafeRetrieval()
    {
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/octocat/Hello-World/pull/20");

        Assert.False(store.TryGetResult(jobId, out var beforeSet));
        Assert.Null(beforeSet);

        var expectedResult = CreateTestResult("Markdown content");
        store.SetResult(jobId, expectedResult);

        Assert.True(store.TryGetResult(jobId, out var afterSet));
        Assert.NotNull(afterSet);
        Assert.Equal("Markdown content", afterSet.Markdown);

        var foundJob = store.TryGetJob(jobId, out var context);
        Assert.True(foundJob);
        Assert.True(context!.IsCompleted);
    }

    [Fact]
    public void SetError_And_TryGetError_RetrievesErrorMessage()
    {
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/octocat/Hello-World/pull/30");

        Assert.False(store.TryGetError(jobId, out var beforeError));
        Assert.Null(beforeError);

        store.SetError(jobId, "GitHub API rate limit exceeded");

        Assert.True(store.TryGetError(jobId, out var error));
        Assert.Equal("GitHub API rate limit exceeded", error);

        var foundJob = store.TryGetJob(jobId, out var context);
        Assert.True(foundJob);
        Assert.True(context!.IsCompleted);
    }

    [Fact]
    public void NonExistentJobs_QueryingAndMutating_ThrowsOrReturnsFalse()
    {
        var store = new ReviewJobStore();
        var missingId = Guid.NewGuid();

        Assert.Throws<KeyNotFoundException>(() => store.GetWriter(missingId));
        Assert.Throws<KeyNotFoundException>(() => store.GetReader(missingId));
        Assert.Throws<KeyNotFoundException>(() => store.SetResult(missingId, CreateTestResult()));
        Assert.Throws<KeyNotFoundException>(() => store.SetError(missingId, "Failed"));

        Assert.False(store.TryGetResult(missingId, out var result));
        Assert.Null(result);

        Assert.False(store.TryGetError(missingId, out var error));
        Assert.Null(error);
    }

    [Fact]
    public async Task ConcurrentAccess_WritingAndReadingAcrossThreads()
    {
        var store = new ReviewJobStore();
        var jobId = store.CreateJob("https://github.com/octocat/Hello-World/pull/99");
        var writer = store.GetWriter(jobId);
        var reader = store.GetReader(jobId);

        const int eventCount = 100;
        var writeTasks = Enumerable.Range(0, 4).Select(workerId => Task.Run(async () =>
        {
            for (var i = 0; i < eventCount; i++)
            {
                await writer.WriteAsync(SseEvent.Phase($"worker_{workerId}", $"msg_{i}"));
            }
        })).ToArray();

        await Task.WhenAll(writeTasks);
        writer.Complete();

        var readCount = 0;
        await foreach (var _ in reader.ReadAllAsync())
        {
            readCount++;
        }

        Assert.Equal(4 * eventCount, readCount);
    }

    [Fact]
    public void SseEvents_FactoryMethods_ConstructExpectedPayloads()
    {
        var finding = new ReviewFinding(PersonaRole.IdiomaticArchitect, Severity.Warning, "Smell", "Refactor", "Foo.cs:10", new[] { "Foo.Bar" });
        var auditItem = new GuardResult("Foo.Bar", true);
        var verdict = CreateTestVerdict();

        var p = SseEvent.Phase("diff", "Fetching diff", 0.5, new { files = 2 });
        Assert.Equal("phase", p.Type);
        var pData = Assert.IsType<PhaseEventData>(p.Data);
        Assert.Equal("diff", pData.Phase);
        Assert.Equal("Fetching diff", pData.Message);
        Assert.Equal(0.5, pData.Progress);

        var ps = SseEvent.PersonaStarted("speedy", "Speedy");
        Assert.Equal("persona_started", ps.Type);
        var psData = Assert.IsType<PersonaStartedEventData>(ps.Data);
        Assert.Equal("speedy", psData.Persona);
        Assert.Equal("Speedy", psData.Character);

        var pf = SseEvent.PersonaFinding("sensei", "Sensei", finding);
        Assert.Equal("persona_finding", pf.Type);
        var pfData = Assert.IsType<PersonaFindingEventData>(pf.Data);
        Assert.Equal(finding, pfData.Finding);

        var pc = SseEvent.PersonaCompleted("sheldon", "Sheldon", 3, "Bazinga!");
        Assert.Equal("persona_completed", pc.Type);
        var pcData = Assert.IsType<PersonaCompletedEventData>(pc.Data);
        Assert.Equal(3, pcData.FindingCount);
        Assert.Equal("Bazinga!", pcData.Quip);

        var pFail = SseEvent.PersonaFailed("speedy", "Speedy", "Timeout");
        Assert.Equal("persona_failed", pFail.Type);
        var pFailData = Assert.IsType<PersonaFailedEventData>(pFail.Data);
        Assert.Equal("Timeout", pFailData.Message);

        var am = SseEvent.ArbiterMerge("Judge", 5, 3, "De-duplicated 2");
        Assert.Equal("arbiter_merge", am.Type);
        var amData = Assert.IsType<ArbiterMergeEventData>(am.Data);
        Assert.Equal(5, amData.TotalRaw);
        Assert.Equal(3, amData.AfterDedupe);

        var ga = SseEvent.GuardAudit("Judge", 3, 0, 3, new[] { auditItem });
        Assert.Equal("guard_audit", ga.Type);
        var gaData = Assert.IsType<GuardAuditEventData>(ga.Data);
        Assert.Equal(3, gaData.Verified);
        Assert.Equal(0, gaData.Stripped);
        Assert.Single(gaData.AuditDetails);

        var comp = SseEvent.Completed("# Report", verdict, new { durationMs = 1200 });
        Assert.Equal("completed", comp.Type);
        var compData = Assert.IsType<CompletedEventData>(comp.Data);
        Assert.Equal("# Report", compData.Markdown);
        Assert.Equal(verdict, compData.Verdict);

        var err = SseEvent.Error("RATE_LIMIT", "Too many requests", "Slow down turtle!");
        Assert.Equal("error", err.Type);
        var errData = Assert.IsType<ErrorEventData>(err.Data);
        Assert.Equal("RATE_LIMIT", errData.Code);
        Assert.Equal("Slow down turtle!", errData.Funny);
    }

    [Fact]
    public void SseEvents_Serialization_ProducesValidJsonWithExpectedKeys()
    {
        var evt = SseEvent.PersonaCompleted("speedy", "Speedy", 2, "Fast!");
        var json = JsonSerializer.Serialize(evt);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("persona_completed", root.GetProperty("type").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("speedy", data.GetProperty("persona").GetString());
        Assert.Equal("Speedy", data.GetProperty("character").GetString());
        Assert.Equal(2, data.GetProperty("findingCount").GetInt32());
        Assert.Equal("Fast!", data.GetProperty("quip").GetString());
        Assert.Equal("persona_completed", data.GetProperty("type").GetString());
    }

    [Fact]
    public void ReviewJobStore_InvalidCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewJobStore(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewJobStore(-5));
    }
}
