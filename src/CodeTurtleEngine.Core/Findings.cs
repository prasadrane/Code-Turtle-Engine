namespace CodeTurtleEngine.Core;

public sealed record ReviewFinding(
    PersonaRole Persona,
    Severity Severity,
    string Title,
    string Detail,
    string Location,
    IReadOnlyList<string> CitedSymbolFqns);

public sealed record PersonaVerdict(
    PersonaRole Persona,
    IReadOnlyList<ReviewFinding> Findings);

public sealed record GuardResult(
    string CitedFqn,
    bool Verified,
    string Evidence = "LegacyUnknown");

public sealed record FindingAudit(ReviewFinding Original, bool Retained, string Status);

public sealed record CouncilVerdict(
    IReadOnlyList<PersonaVerdict> Personas,
    IReadOnlyList<ReviewFinding> Synthesized,
    IReadOnlyList<GuardResult> GuardAudit)
{
    public IReadOnlyList<FindingAudit> FindingAudit { get; init; } = Array.Empty<FindingAudit>();
}
