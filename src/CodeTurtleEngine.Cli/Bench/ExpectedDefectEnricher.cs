using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Cli.Bench;

/// <summary>Fills ExpectedDefect.Method with the enclosing method/constructor of each labeled line.</summary>
public static class ExpectedDefectEnricher
{
    public static IReadOnlyList<ExpectedDefect>? Enrich(Compilation compilation, IReadOnlyList<ExpectedDefect>? expected)
    {
        if (expected is null) return null;
        return expected.Select(d =>
        {
            if (d.Method is not null) return d;
            var tree = compilation.SyntaxTrees.FirstOrDefault(t => RepoPaths.EndsWith(t.FilePath, d.File));
            if (tree is null) return d;
            var root = tree.GetRoot();
            var text = tree.GetText();
            if (d.Line < 1 || d.Line > text.Lines.Count) return d;
            var line = text.Lines[d.Line - 1];
            var pos = line.Start;
            while (pos < line.End && char.IsWhiteSpace(text[pos])) pos++;
            var name = root.DescendantNodes()
                .Where(n => n is MethodDeclarationSyntax or ConstructorDeclarationSyntax)
                .Where(n => n.Span.Contains(pos))
                .Select(n => n is MethodDeclarationSyntax m ? m.Identifier.Text : ((ConstructorDeclarationSyntax)n).Identifier.Text)
                .LastOrDefault();
            return name is null ? d : d with { Method = name };
        }).ToList();
    }
}
