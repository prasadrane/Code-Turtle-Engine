namespace CodeTurtleEngine.Core;

public sealed record MethodFacts(
    string Method,
    IReadOnlyList<string> Allocations,
    string? AsyncHealth,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> ResolvedSymbols);

public sealed record FileFacts(
    string FilePath,
    IReadOnlyList<MethodFacts> Methods);

public sealed record RoslynPayload(
    string RepoSlug,
    string DiffBaseline,
    IReadOnlyList<FileFacts> Files);
