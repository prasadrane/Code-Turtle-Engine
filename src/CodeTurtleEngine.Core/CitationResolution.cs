namespace CodeTurtleEngine.Core;

/// <summary>Declaration identity, not evidence that a recommendation or call site is correct.</summary>
public sealed record CitationSymbol(string Assembly, string DocumentationId, string Display);

public sealed record CitationResolution(bool Exists, string Match, IReadOnlyList<CitationSymbol> Symbols);

/// <summary>Name membership in extracted facts; not an invocation or defect location.</summary>
public sealed record CitationPayloadContext(string File, string Member, int StartLine, int EndLine);
