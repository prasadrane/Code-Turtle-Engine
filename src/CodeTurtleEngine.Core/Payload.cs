namespace CodeTurtleEngine.Core;

public sealed record MethodFacts(
    string Method,
    IReadOnlyList<string> Allocations,
    string? AsyncHealth,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> ResolvedSymbols,
    int StartLine = 0,
    int EndLine = 0,
    IReadOnlyList<string>? Risks = null);

public sealed record FileFacts(
    string FilePath,
    IReadOnlyList<MethodFacts> Methods);

public sealed record RoslynPayload(
    string RepoSlug,
    string DiffBaseline,
    IReadOnlyList<FileFacts> Files);

/// <summary>Changed-file source text; used only by the naive (benchmark baseline) prompt.</summary>
public sealed record SourceFile(string Path, string Text);
