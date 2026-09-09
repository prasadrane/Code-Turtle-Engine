using CodeTurtleEngine.Core;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Council;

public interface IPersonaRunner
{
    Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default);
}

public sealed class PersonaRunner : IPersonaRunner
{
    private static readonly PersonaRole[] ReviewPersonas =
    {
        PersonaRole.AllocationsPerformance,
        PersonaRole.SecurityAuditor,
        PersonaRole.IdiomaticArchitect
    };

    private readonly IStructuredChatClient _chat;
    private readonly CouncilOptions _options;

    public PersonaRunner(IStructuredChatClient chat, CouncilOptions options)
    {
        _chat = chat;
        _options = options;
    }

    public async Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default)
    {
        var results = await Task.WhenAll(ReviewPersonas.Select(role => RunOneAsync(role, payload, rubric, ct))).ConfigureAwait(false);
        var succeeded = results.Where(r => r is not null).Select(r => r!).ToList();

        if (succeeded.Count < _options.Quorum)
            throw new ProviderException(
                $"Council quorum not met: {succeeded.Count}/{ReviewPersonas.Length} personas succeeded (quorum {_options.Quorum}).",
                new[] { $"{ReviewPersonas.Length - succeeded.Count} persona call(s) failed" });

        return succeeded;
    }

    private async Task<PersonaVerdict?> RunOneAsync(PersonaRole role, RoslynPayload payload, string rubric, CancellationToken ct)
    {
        try
        {
            var msgs = PromptBuilder.Build(role, payload, rubric, _options.MaxFindingsPerPersona);
            var dto = await _chat.CompleteStructuredAsync<VerdictDto>(ModelRoleFor(role), msgs, PromptBuilder.VerdictSchema, ct).ConfigureAwait(false);
            var findings = dto.Findings
                .Select(f => new ReviewFinding(role, f.Severity, f.Title, f.Detail, f.Location, f.CitedSymbolFqns))
                .ToList();
            return new PersonaVerdict(role, findings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // user cancellation must surface, not be swallowed into a null persona result
        }
        catch (Exception ex)
        {
            // Observability: surface why a persona failed (the final review flagged the silent swallow).
            // For ProviderException, include the per-route attempt log — the real transport cause.
            // TODO Phase 2: replace Console.Error with a proper ILogger.
            var detail = ex is ProviderException pe && pe.AttemptLog.Count > 0
                ? string.Join(" | ", pe.AttemptLog)
                : ex.Message;
            Console.Error.WriteLine($"[turtle] persona {role} failed: {ex.GetType().Name}: {detail}");
            return null;
        }
    }

    public static string ModelRoleFor(PersonaRole role) =>
        role == PersonaRole.AllocationsPerformance ? "fast" : "deep";
}
