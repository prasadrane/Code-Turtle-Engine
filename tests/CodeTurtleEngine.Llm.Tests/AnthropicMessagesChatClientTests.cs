using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CodeTurtleEngine.Llm.Tests;

public sealed class AnthropicMessagesChatClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string CapturedBody { get; private set; } = "";

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content is not null)
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request);
        }
    }

    private const string CannedResponse = """
        {"id":"msg_1","type":"message","role":"assistant","model":"qwen3.8-flash",
         "content":[{"type":"thinking","thinking":"let me think about this"},
                    {"type":"text","text":"pong"}],
         "stop_reason":"end_turn","usage":{"input_tokens":5,"output_tokens":3}}
        """;

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static AnthropicMessagesChatClient Client(StubHandler handler)
        => new("https://relay.test/apps/anthropic", "test-key", "qwen3.8-flash", handler: handler);

    [Fact]
    public async Task Returns_Concatenated_Text_Blocks_Ignoring_Thinking()
    {
        var handler = new StubHandler(_ => Json(CannedResponse));
        var resp = await Client(handler).GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "ping") });
        Assert.Equal("pong", resp.Text);
    }

    [Fact]
    public async Task Sends_Post_To_Messages_Endpoint_With_Anthropic_Headers()
    {
        var handler = new StubHandler(_ => Json(CannedResponse));
        await Client(handler).GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });

        var req = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/messages", req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", req.Headers.Authorization.Parameter);
        Assert.Equal("2023-06-01", req.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public async Task Body_Maps_System_To_TopLevel_And_Keeps_Turn_Roles()
    {
        var handler = new StubHandler(_ => Json(CannedResponse));
        var client = new AnthropicMessagesChatClient(
            "https://relay.test/apps/anthropic/", "k", "qwen3.8-max", 1234, handler);
        await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.System, "be terse"),
            new ChatMessage(ChatRole.User, "ping"),
            new ChatMessage(ChatRole.Assistant, "pong"),
            new ChatMessage(ChatRole.User, "again")
        });

        using var doc = JsonDocument.Parse(handler.CapturedBody);
        var root = doc.RootElement;
        Assert.Equal("qwen3.8-max", root.GetProperty("model").GetString());
        Assert.Equal(1234, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("be terse", root.GetProperty("system").GetString());
        var msgs = root.GetProperty("messages");
        Assert.Equal(3, msgs.GetArrayLength());
        Assert.Equal("user", msgs[0].GetProperty("role").GetString());
        Assert.Equal("ping", msgs[0].GetProperty("content").GetString());
        Assert.Equal("assistant", msgs[1].GetProperty("role").GetString());
        Assert.Equal("user", msgs[2].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Omits_System_When_No_System_Message()
    {
        var handler = new StubHandler(_ => Json(CannedResponse));
        await Client(handler).GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });
        using var doc = JsonDocument.Parse(handler.CapturedBody);
        Assert.False(doc.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public async Task Throws_HttpRequestException_Carrying_Status_On_Non_200()
    {
        var handler = new StubHandler(_ => Json("{\"error\":\"slow down\"}", HttpStatusCode.TooManyRequests));
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(handler).GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }
}
