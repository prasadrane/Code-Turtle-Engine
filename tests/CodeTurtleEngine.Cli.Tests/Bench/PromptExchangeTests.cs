using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class PromptExchangeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "turtle-exch-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static IReadOnlyList<ChatMessage> Msgs(string system, string user) =>
        new[] { new ChatMessage(ChatRole.System, system), new ChatMessage(ChatRole.User, user) };

    [Fact]
    public void Key_Is_Stable_And_Sensitive_To_Both_Messages()
    {
        var a = PromptExchange.Key(Msgs("s", "u"));
        Assert.Equal(a, PromptExchange.Key(Msgs("s", "u")));
        Assert.NotEqual(a, PromptExchange.Key(Msgs("s2", "u")));
        Assert.NotEqual(a, PromptExchange.Key(Msgs("s", "u2")));
        Assert.Equal(16, a.Length);
    }

    [Fact]
    public async Task Recording_Writes_Each_Unique_Prompt_Once_And_Returns_Empty_Verdict()
    {
        var rec = new RecordingChat(_dir);
        var msgs = Msgs("persona rules", "source text");

        var first = await rec.CompleteStructuredAsync<VerdictDto>("fast", msgs, "{\"type\":\"object\"}");
        await rec.CompleteStructuredAsync<VerdictDto>("fast", msgs, "{\"type\":\"object\"}");

        Assert.Empty(first.Findings);
        Assert.Equal(1, rec.PromptCount);
        var prompt = File.ReadAllText(Path.Combine(_dir, PromptExchange.Key(msgs), "prompt.md"));
        Assert.Contains("persona rules", prompt);
        Assert.Contains("source text", prompt);
        Assert.Contains("OUTPUT SCHEMA", prompt);
    }

    [Fact]
    public async Task Replay_Requires_SelectRepetition_Before_Serving_Answers()
    {
        var msgs = Msgs("s", "u");
        var keyDir = Path.Combine(_dir, PromptExchange.Key(msgs));
        Directory.CreateDirectory(keyDir);
        File.WriteAllText(Path.Combine(keyDir, "1.json"), "{\"Findings\":[]}");
        var replay = new ReplayChat(_dir);

        var ex = await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}"));
        Assert.Contains("SelectRepetition", ex.Message);
    }

    [Fact]
    public async Task Replay_Serves_Selected_Repetition_And_Accepts_Case_Insensitive_Json()
    {
        var msgs = Msgs("s", "u");
        var keyDir = Path.Combine(_dir, PromptExchange.Key(msgs));
        Directory.CreateDirectory(keyDir);
        File.WriteAllText(Path.Combine(keyDir, "1.json"),
            "{\"Findings\":[{\"Severity\":\"Warning\",\"Title\":\"t1\",\"Detail\":\"d\",\"Location\":\"A.cs:1\",\"CitedSymbolFqns\":[\"X\"]}]}");
        File.WriteAllText(Path.Combine(keyDir, "2.json"),
            "{\"findings\":[{\"severity\":\"Error\",\"title\":\"t2\",\"detail\":\"d\",\"location\":\"A.cs:2\",\"citedSymbolFqns\":[]}]}");
        var replay = new ReplayChat(_dir);

        replay.SelectRepetition(1);
        var one = await replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}");
        replay.SelectRepetition(2);
        var two = await replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}");

        Assert.Equal("t1", one.Findings[0].Title);
        Assert.Equal("t2", two.Findings[0].Title);
        replay.SelectRepetition(3);
        await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}"));
    }

    [Fact]
    public async Task Explicit_Repetition_Reuses_Identical_Prompts_Without_Advancing_The_Sample()
    {
        var msgs = Msgs("s", "u");
        var keyDir = Path.Combine(_dir, PromptExchange.Key(msgs));
        Directory.CreateDirectory(keyDir);
        File.WriteAllText(Path.Combine(keyDir, "2.json"), "{\"Findings\":[]}");
        var replay = new ReplayChat(_dir);
        replay.SelectRepetition(2);
        Assert.Empty((await replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}")).Findings);
        Assert.Empty((await replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}")).Findings);
        replay.SelectRepetition(3);
        await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}"));
    }

    [Fact]
    public async Task Replay_Fails_When_Prompt_Has_No_Recorded_Answer()
    {
        var replay = new ReplayChat(_dir);
        replay.SelectRepetition(1);
        await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", Msgs("never", "seen"), "{}"));
    }

    [Fact]
    public async Task Replay_Rejects_Malformed_Answer()
    {
        var msgs = Msgs("s", "u");
        var keyDir = Path.Combine(_dir, PromptExchange.Key(msgs));
        Directory.CreateDirectory(keyDir);
        File.WriteAllText(Path.Combine(keyDir, "1.json"), "not json");
        var replay = new ReplayChat(_dir);
        replay.SelectRepetition(1);

        await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}"));
    }

    [Fact]
    public async Task Replay_Reads_Provenance_Manifest_And_Verifies_Answer_Hashes()
    {
        var msgs = Msgs("s", "u");
        var key = PromptExchange.Key(msgs);
        var keyDir = Path.Combine(_dir, key);
        Directory.CreateDirectory(keyDir);
        File.WriteAllText(Path.Combine(keyDir, "1.json"), "{\"Findings\":[]}");
        File.WriteAllText(Path.Combine(_dir, "provenance.json"),
            $$"""
            {
              "model": "claude-sonnet",
              "harness": "claude-code",
              "provenance": "reconstructed-from-session",
              "prompts": [
                {
                  "key": "{{key}}",
                  "promptSha256": "abc",
                  "answers": [
                    { "repetition": 1, "answerSha256": "wrong-hash" }
                  ]
                }
              ]
            }
            """);

        var replay = new ReplayChat(_dir);
        Assert.Contains("claude-sonnet via claude-code, reconstructed-from-session", replay.DescribeRoute());
        replay.SelectRepetition(1);
        var ex = await Assert.ThrowsAsync<ModelException>(() =>
            replay.CompleteStructuredAsync<VerdictDto>("deep", msgs, "{}"));
        Assert.Contains("SHA-256", ex.Message);
    }
}
