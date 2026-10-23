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
        // Dots inside generic arguments do not separate the declaring type/member.
        int depth = 0, memberDot = -1;
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == '<') depth++;
            else if (name[i] == '>') depth--;
            else if (name[i] == '.' && depth == 0) memberDot = i;
        }
        var generic = name.IndexOf('<', memberDot + 1);
        if (generic >= 0) name = name[..generic];
        return payload.Files.SelectMany(f => f.Methods.Where(m => m.ResolvedSymbols.Contains(name, StringComparer.Ordinal))
            .Select(m => new CitationPayloadContext(f.FilePath, m.Method, m.StartLine, m.EndLine)))
            .Distinct().ToList();
    }
}
