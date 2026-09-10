using System.Text.Json.Serialization;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Web.Models;

public static class SseEventTypes
{
    public const string Phase = "phase";
    public const string PersonaStarted = "persona_started";
    public const string PersonaFinding = "persona_finding";
    public const string PersonaCompleted = "persona_completed";
    public const string PersonaFailed = "persona_failed";
    public const string ArbiterMerge = "arbiter_merge";
    public const string GuardAudit = "guard_audit";
    public const string Completed = "completed";
    public const string Error = "error";
}

public sealed record PhaseEventData(
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("progress")] double? Progress = null,
    [property: JsonPropertyName("stats")] object? Stats = null)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.Phase;
}

public sealed record PersonaStartedEventData(
    [property: JsonPropertyName("persona")] string Persona,
    [property: JsonPropertyName("character")] string Character)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.PersonaStarted;
}

public sealed record PersonaFindingEventData(
    [property: JsonPropertyName("persona")] string Persona,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("finding")] ReviewFinding Finding)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.PersonaFinding;
}

public sealed record PersonaCompletedEventData(
    [property: JsonPropertyName("persona")] string Persona,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("findingCount")] int FindingCount,
    [property: JsonPropertyName("quip")] string? Quip = null)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.PersonaCompleted;
}

public sealed record PersonaFailedEventData(
    [property: JsonPropertyName("persona")] string Persona,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("message")] string Message)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.PersonaFailed;
}

public sealed record ArbiterMergeEventData(
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("totalRaw")] int TotalRaw,
    [property: JsonPropertyName("afterDedupe")] int AfterDedupe,
    [property: JsonPropertyName("quip")] string? Quip = null)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.ArbiterMerge;
}

public sealed record GuardAuditEventData(
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("verified")] int Verified,
    [property: JsonPropertyName("stripped")] int Stripped,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("auditDetails")] IReadOnlyList<GuardResult> AuditDetails)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.GuardAudit;
}

public sealed record CompletedEventData(
    [property: JsonPropertyName("markdown")] string Markdown,
    [property: JsonPropertyName("verdict")] CouncilVerdict Verdict,
    [property: JsonPropertyName("stats")] object? Stats = null)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.Completed;
}

public sealed record ErrorEventData(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("funny")] string? Funny = null)
{
    [JsonPropertyName("type")]
    public string Type => SseEventTypes.Error;
}

public record SseEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] object Data)
{
    public const string PhaseType = SseEventTypes.Phase;
    public const string PersonaStartedType = SseEventTypes.PersonaStarted;
    public const string PersonaFindingType = SseEventTypes.PersonaFinding;
    public const string PersonaCompletedType = SseEventTypes.PersonaCompleted;
    public const string PersonaFailedType = SseEventTypes.PersonaFailed;
    public const string ArbiterMergeType = SseEventTypes.ArbiterMerge;
    public const string GuardAuditType = SseEventTypes.GuardAudit;
    public const string CompletedType = SseEventTypes.Completed;
    public const string ErrorType = SseEventTypes.Error;

    public static SseEvent Phase(string phase, string message, double? progress = null, object? stats = null) =>
        new(SseEventTypes.Phase, new PhaseEventData(phase, message, progress, stats));

    public static SseEvent PersonaStarted(string persona, string character) =>
        new(SseEventTypes.PersonaStarted, new PersonaStartedEventData(persona, character));

    public static SseEvent PersonaFinding(string persona, string character, ReviewFinding finding) =>
        new(SseEventTypes.PersonaFinding, new PersonaFindingEventData(persona, character, finding));

    public static SseEvent PersonaCompleted(string persona, string character, int findingCount, string? quip = null) =>
        new(SseEventTypes.PersonaCompleted, new PersonaCompletedEventData(persona, character, findingCount, quip));

    public static SseEvent PersonaFailed(string persona, string character, string message) =>
        new(SseEventTypes.PersonaFailed, new PersonaFailedEventData(persona, character, message));

    public static SseEvent ArbiterMerge(string character, int totalRaw, int afterDedupe, string? quip = null) =>
        new(SseEventTypes.ArbiterMerge, new ArbiterMergeEventData(character, totalRaw, afterDedupe, quip));

    public static SseEvent GuardAudit(string character, int verified, int stripped, int total, IReadOnlyList<GuardResult> auditDetails) =>
        new(SseEventTypes.GuardAudit, new GuardAuditEventData(character, verified, stripped, total, auditDetails));

    public static SseEvent Completed(string markdown, CouncilVerdict verdict, object? stats = null) =>
        new(SseEventTypes.Completed, new CompletedEventData(markdown, verdict, stats));

    public static SseEvent Error(string code, string message, string? funny = null) =>
        new(SseEventTypes.Error, new ErrorEventData(code, message, funny));
}
