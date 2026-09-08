using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

public sealed record FindingDto(
    Severity Severity,
    string Title,
    string Detail,
    string Location,
    IReadOnlyList<string> CitedSymbolFqns);

public sealed record VerdictDto(IReadOnlyList<FindingDto> Findings);
