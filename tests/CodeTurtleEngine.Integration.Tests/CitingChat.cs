using CodeTurtleEngine.Council;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.AI;

namespace CodeTurtleEngine.Integration.Tests;

// Returns one grounded finding (cites a real symbol) and one hallucinated finding.
internal sealed class CitingChat : IStructuredChatClient
{
    private readonly string _realSymbol;

    public CitingChat(string realSymbol) => _realSymbol = realSymbol;

    public Task<T> CompleteStructuredAsync<T>(string role, IReadOnlyList<ChatMessage> messages,
        string jsonSchema, CancellationToken ct = default)
    {
        var dto = new VerdictDto(new[]
        {
            new FindingDto(Severity.Warning, "Async health", "Missing ConfigureAwait(false).",
                "PaymentService.cs:14", new[] { _realSymbol }),
            new FindingDto(Severity.Error, "Phantom API", "Calls a method that does not exist.",
                "PaymentService.cs:99", new[] { "SampleRepo.DoesNotExist" })
        });
        return Task.FromResult((T)(object)dto);
    }
}
