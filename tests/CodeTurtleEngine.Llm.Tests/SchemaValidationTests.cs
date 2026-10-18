using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;
namespace CodeTurtleEngine.Llm.Tests;
public class SchemaValidationTests
{
    private const string Schema = """
    {"type":"object","required":["Findings"],"additionalProperties":false,"properties":{
    "Findings":{"type":"array","items":{"type":"object","required":["Severity","Citations"],
    "additionalProperties":false,"properties":{"Severity":{"type":"string","enum":["Warning"]},
    "Citations":{"type":"array","items":{"type":"string"}}}}}}}
    """;
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"Findings\":null}")]
    [InlineData("{\"Findings\":{}}")]
    [InlineData("{\"Findings\":[{}]}")]
    [InlineData("{\"Findings\":[{\"Severity\":1,\"Citations\":[]}]}")]
    [InlineData("{\"Findings\":[{\"Severity\":\"Critical\",\"Citations\":[]}]}")]
    [InlineData("{\"Findings\":[{\"Severity\":\"Warning\",\"Citations\":[null]}]}")]
    [InlineData("{\"Findings\":[],\"Extra\":true}")]
    [InlineData("{\"Findings\":[],\"Findings\":[]}")]
    public async Task Invalid_Output_Is_Model_Failure_With_Bounded_Retry(string json)
    {
        var calls = 0;
        var gateway = new FakeGateway((_, _, _) => { calls++; return Task.FromResult(json); });
        await Assert.ThrowsAsync<ModelException>(() => new StructuredChatClient(gateway, 1)
            .CompleteStructuredAsync<object>("fast", Array.Empty<ChatMessage>(), Schema));
        Assert.Equal(2, calls);
    }
    [Fact]
    public async Task Cancellation_Does_Not_Fallback_Or_Retry()
    {
        var calls = 0;
        var gateway = new FakeGateway((_, _, _) => { calls++; throw new OperationCanceledException(); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => new StructuredChatClient(gateway, 2)
            .CompleteStructuredAsync<object>("fast", Array.Empty<ChatMessage>(), Schema));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Unrelated_Exception_Does_Not_Fallback()
    {
        var calls = 0;
        var gateway = new FakeGateway((_, _, _) => { calls++; throw new InvalidOperationException("network down"); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StructuredChatClient(gateway)
            .CompleteStructuredAsync<object>("fast", Array.Empty<ChatMessage>(), Schema));
        Assert.Equal(1, calls);
    }
}
