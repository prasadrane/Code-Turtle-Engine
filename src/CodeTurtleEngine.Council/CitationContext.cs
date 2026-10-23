using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

internal static class CitationContext
{
    public static IReadOnlyList<CitationPayloadContext> Find(RoslynPayload payload, string citation)
    {
        // Extractor facts contain names, not overload identities or invocation spans.
        var name = citation.Trim();
        if (name.StartsWith("global::", StringComparison.Ordinal)) name = name[8..];
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        var generic = name.LastIndexOf('<');
        if (generic > name.LastIndexOf('.')) name = name[..generic];
        return payload.Files.SelectMany(f => f.Methods.Where(m => m.ResolvedSymbols.Contains(name, StringComparer.Ordinal))
            .Select(m => new CitationPayloadContext(f.FilePath, m.Method, m.StartLine, m.EndLine)))
            .Distinct().ToList();
    }
}
