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
