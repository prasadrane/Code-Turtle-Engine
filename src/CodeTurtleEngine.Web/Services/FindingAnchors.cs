using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Web.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Web.Services;

public static class FindingAnchors
{
    public static ReviewChanges Attach(ReviewChanges changes, RoslynPayload payload, IReadOnlyList<ReviewFinding> findings,
        IReadOnlyList<Compilation>? projects = null)
    {
        var anchors = new List<FindingAnchor>();
        var declarations = new List<(string Path, string Symbol, int Start, int End)>();
        var uniqueTypes = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var file in changes.Files.Where(f => f.Analyzed && f.Source is not null))
        {
            var contexts = payload.Files.Where(f => f.FilePath == file.Path).ToList();
            if (contexts.Count != 1) continue; // Linked files across projects remain ambiguous.
            var model = ContextModel(file, projects);
            if (model is null) continue;
            var tree = model.SyntaxTree;
            foreach (var node in tree.GetRoot().DescendantNodes().Where(n => n is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax))
            {
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol?.ContainingType is null) continue;
                var span = node.GetLocation().GetLineSpan();
                var start = span.StartLinePosition.Line + 1;
                var end = span.EndLinePosition.Line + 1;
                var name = symbol.ContainingType.ToDisplayString() + "." + symbol.Name;
                var facts = contexts[0].Methods.Where(m => m.StartLine == start && m.EndLine == end && m.ResolvedSymbols.Contains(name)).ToList();
                if (facts.Count != 1 || payload.Files.SelectMany(f => f.Methods).Count(m =>
                    m.Method == facts[0].Method && m.ResolvedSymbols.Contains(name)) != 1) continue;
                if (symbol.ContainingType.GetMembers(symbol.Name).Count(s => s.Locations.Any(l => l.IsInSource)) != 1) continue;
                if (projects is not null)
                {
                    var typeName = symbol.ContainingType.ToDisplayString();
                    if (!uniqueTypes.TryGetValue(typeName, out var unique))
                    {
                        unique = projects.SelectMany(project => project.GetSymbolsWithName(symbol.ContainingType.Name, SymbolFilter.Type))
                            .OfType<INamedTypeSymbol>().Where(type => type.ToDisplayString() == typeName && type.Locations.Any(l => l.IsInSource))
                            .Distinct(SymbolEqualityComparer.Default).Count() == 1;
                        uniqueTypes[typeName] = unique;
                    }
                    if (!unique) continue;
                }
                declarations.Add((file.Path, name, start, end));
            }
        }
        for (var index = 0; index < findings.Count; index++)
        {
            // Resolve the DECLARED member only. A called/resolved symbol elsewhere in a method
            // is not evidence that the recommendation belongs at that method's location.
            var candidates = declarations.Where(declaration => findings[index].CitedSymbolFqns.Contains(declaration.Symbol, StringComparer.Ordinal)).ToList();
            if (candidates.Count != 1) continue; // Includes overload and multi-project ambiguity.
            var member = candidates[0];
            anchors.Add(new FindingAnchor(index, member.Path, member.Start, member.End, "RoslynMember"));
        }
        return changes with { Anchors = anchors };
    }

    private static SemanticModel? ContextModel(ChangedFile file, IReadOnlyList<Compilation>? projects)
    {
        if (projects is null)
        {
            var tree = CSharpSyntaxTree.ParseText(file.Source!);
            return CSharpCompilation.Create("DisplayContext", [tree]).GetSemanticModel(tree);
        }
        var matches = projects.SelectMany(project => project.SyntaxTrees.Where(tree => RepoPaths.EndsWith(tree.FilePath, file.Path))
            .Select(tree => (Project: project, Tree: tree))).ToList();
        if (matches.Count != 1 || matches[0].Tree.GetText().ToString() != file.Source) return null;
        return matches[0].Project.GetSemanticModel(matches[0].Tree);
    }
}
