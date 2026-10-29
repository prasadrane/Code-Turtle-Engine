using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record FindingDto(
    Severity Severity,
    string Title,
    string Detail,
    string Location,
    IReadOnlyList<string> CitedSymbolFqns,
    string? OriginalCode = null,
    string? SuggestedCode = null);

public sealed record VerdictDto(IReadOnlyList<FindingDto> Findings);
