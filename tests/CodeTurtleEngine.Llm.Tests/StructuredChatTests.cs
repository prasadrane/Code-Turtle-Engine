using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Llm.Tests;

public class StructuredChatTests
{
    private sealed record Dto(string Title, int Severity);

    private static IReadOnlyList<ChatMessage> Msgs() => new[] { new ChatMessage(ChatRole.User, "hi") };
    private static ILlmGateway Returning(string text) => new FakeGateway((_, _, _) => Task.FromResult(text));

    [Fact]
    public async Task Parses_Valid_Json()
    {
        var sc = new StructuredChatClient(Returning("{\"Title\":\"x\",\"Severity\":2}"));
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("x", dto.Title);
        Assert.Equal(2, dto.Severity);
    }

    [Fact]
    public async Task Extracts_Json_From_Prose()
    {
        var sc = new StructuredChatClient(Returning("Sure: {\"Title\":\"y\",\"Severity\":1} done"));
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("y", dto.Title);
    }

    [Fact]
    public async Task Throws_ModelException_On_Garbage()
    {
        var sc = new StructuredChatClient(Returning("not json at all"));
        await Assert.ThrowsAsync<ModelException>(() => sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}"));
    }

    [Fact]
    public async Task Schema_Instruction_Always_In_Prompt()
    {
        var calls = new List<IReadOnlyList<ChatMessage>>();
        var gw = new FakeGateway((_, m, o) =>
        {
            calls.Add(m);
            // First call carries the OpenAI response_format attempt; degrade path drops it.
            return o?.ResponseFormat is not null
                ? throw new InvalidOperationException("schema unsupported")
                : Task.FromResult("{\"Title\":\"x\",\"Severity\":1}");
        });
        var sc = new StructuredChatClient(gw);
        await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{\"type\":\"object\"}");

        Assert.Equal(2, calls.Count);
        foreach (var sent in calls)
            Assert.Contains(sent, m => (m.Text ?? "").Contains("JSON")
                && (m.Text ?? "").Contains("{\"type\":\"object\"}"));
    }

    [Fact]
    public async Task Retries_On_Malformed_Json_Then_Succeeds()
    {
        var calls = 0;
        var gw = new FakeGateway((_, _, _) => Task.FromResult(++calls == 1
            ? "not json"
            : "{\"Title\":\"ok\",\"Severity\":1}"));
        var sc = new StructuredChatClient(gw, jsonRetries: 1);

        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");

        Assert.Equal("ok", dto.Title);
        Assert.Equal(2, calls); // one re-roll after the malformed first response
    }

    [Fact]
    public async Task Throws_ModelException_After_Retries_Exhausted()
    {
        var calls = 0;
        var gw = new FakeGateway((_, _, _) =>
        {
            calls++;
            return Task.FromResult("still not json");
        });
        var sc = new StructuredChatClient(gw, jsonRetries: 2);

        await Assert.ThrowsAsync<ModelException>(
            () => sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}"));

        Assert.Equal(3, calls); // initial attempt + jsonRetries re-rolls
    }

    [Fact]
    public async Task Does_Not_Retry_On_Transport_ProviderException()
    {
        var calls = 0;
        var gw = new FakeGateway((_, _, _) =>
        {
            calls++;
            return Task.FromException<string>(new ProviderException("down", new[] { "x" }, null));
        });
        var sc = new StructuredChatClient(gw, jsonRetries: 2);

        await Assert.ThrowsAsync<ProviderException>(
            () => sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}"));

        Assert.Equal(2, calls); // schema attempt + degrade path only, no JSON re-rolls
    }

    [Fact]
    public async Task Degrades_When_Schema_Mode_Unsupported()
    {
        var gw = new FakeGateway((_, _, o) => o?.ResponseFormat is not null
            ? throw new InvalidOperationException("schema unsupported")
            : Task.FromResult("{\"Title\":\"z\",\"Severity\":3}"));
        var sc = new StructuredChatClient(gw);
        var dto = await sc.CompleteStructuredAsync<Dto>("fast", Msgs(), "{}");
        Assert.Equal("z", dto.Title);
    }
}
