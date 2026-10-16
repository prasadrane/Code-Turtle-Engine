using System.Collections.Concurrent;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Council.Tests;

public class PersonaRunnerModeTests
{
    private sealed class ModeCapturingChat : IStructuredChatClient
    {
        public ConcurrentBag<string> UserMessages { get; } = new();

        public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
            string jsonSchema, CancellationToken ct = default)
        {
            UserMessages.Add(messages[^1].Text);
            return Task.FromResult((T)(object)new VerdictDto(Array.Empty<FindingDto>()));
        }
    }

    private static readonly RoslynPayload Payload = new("r", "HEAD", Array.Empty<FileFacts>());
    private static readonly SourceFile[] Sources = { new("A.cs", "class A { /* marker-src */ }") };

    [Fact]
    public async Task Naive_Mode_Sends_Source_Text()
    {
        var chat = new ModeCapturingChat();
        await new PersonaRunner(chat, new CouncilOptions { PromptMode = PromptMode.Naive })
            .RunAsync(Payload, Sources, "# Rubric");

        Assert.Equal(3, chat.UserMessages.Count);
        Assert.All(chat.UserMessages, m => Assert.Contains("marker-src", m));
    }

    [Fact]
    public async Task Grounded_Mode_Sends_Payload_Json_Not_Source()
    {
        var chat = new ModeCapturingChat();
        await new PersonaRunner(chat, new CouncilOptions()).RunAsync(Payload, Sources, "# Rubric");

        Assert.All(chat.UserMessages, m =>
        {
            Assert.DoesNotContain("marker-src", m);
            Assert.Contains("\"RepoSlug\"", m);
        });
    }
}
