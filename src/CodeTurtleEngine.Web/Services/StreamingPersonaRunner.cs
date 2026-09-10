using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;

namespace CodeTurtleEngine.Web.Services;

public interface IStreamingPersonaRunner
{
    Task<IReadOnlyList<PersonaVerdict>> RunWithCallbacksAsync(
        RoslynPayload payload,
        string rubric,
        Func<PersonaRole, Task> onStarted,
        Func<PersonaVerdict, Task> onCompleted,
        Func<PersonaRole, string, Task> onFailed,
        CancellationToken ct = default);
}

public sealed class StreamingPersonaRunner : IStreamingPersonaRunner
{
    private static readonly PersonaRole[] ReviewPersonas =
    {
        PersonaRole.AllocationsPerformance,
        PersonaRole.SecurityAuditor,
        PersonaRole.IdiomaticArchitect
    };

    private readonly IStructuredChatClient _chat;
    private readonly CouncilOptions _options;

    public StreamingPersonaRunner(IStructuredChatClient chat, CouncilOptions options)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<PersonaVerdict>> RunWithCallbacksAsync(
        RoslynPayload payload,
        string rubric,
        Func<PersonaRole, Task> onStarted,
        Func<PersonaVerdict, Task> onCompleted,
        Func<PersonaRole, string, Task> onFailed,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(rubric);
        ArgumentNullException.ThrowIfNull(onStarted);
        ArgumentNullException.ThrowIfNull(onCompleted);
        ArgumentNullException.ThrowIfNull(onFailed);

        var tasks = ReviewPersonas.Select(role => RunOneAsync(role, payload, rubric, onStarted, onCompleted, onFailed, ct));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var succeeded = results.Where(r => r is not null).Select(r => r!).ToList();

        if (succeeded.Count < _options.Quorum)
        {
            throw new ProviderException(
                $"Council quorum not met: {succeeded.Count}/{ReviewPersonas.Length} personas succeeded (quorum {_options.Quorum}).",
                new[] { $"{ReviewPersonas.Length - succeeded.Count} persona call(s) failed" });
        }

        return succeeded;
    }

    private async Task<PersonaVerdict?> RunOneAsync(
        PersonaRole role,
        RoslynPayload payload,
        string rubric,
        Func<PersonaRole, Task> onStarted,
        Func<PersonaVerdict, Task> onCompleted,
        Func<PersonaRole, string, Task> onFailed,
        CancellationToken ct)
    {
        try
        {
            await onStarted(role).ConfigureAwait(false);
            var msgs = PromptBuilder.Build(role, payload, rubric, _options.MaxFindingsPerPersona);
            var dto = await _chat.CompleteStructuredAsync<VerdictDto>(ModelRoleFor(role), msgs, PromptBuilder.VerdictSchema, ct).ConfigureAwait(false);
            var findings = dto.Findings
                .Select(f => new ReviewFinding(role, f.Severity, f.Title, f.Detail, f.Location, f.CitedSymbolFqns))
                .ToList();
            var verdict = new PersonaVerdict(role, findings);
            await onCompleted(verdict).ConfigureAwait(false);
            return verdict;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var detail = ex is ProviderException pe && pe.AttemptLog.Count > 0
                ? string.Join(" | ", pe.AttemptLog)
                : ex.Message;
            Console.Error.WriteLine($"[turtle] persona {role} failed: {ex.GetType().Name}: {detail}");
            try
            {
                await onFailed(role, detail).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception cbEx)
            {
                Console.Error.WriteLine($"[turtle] onFailed callback for {role} threw: {cbEx.GetType().Name}: {cbEx.Message}");
            }
            return null;
        }
    }

    public static string ModelRoleFor(PersonaRole role) =>
        role == PersonaRole.AllocationsPerformance ? "fast" : "deep";
}
