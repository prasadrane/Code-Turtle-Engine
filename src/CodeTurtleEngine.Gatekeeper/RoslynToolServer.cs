using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record SymbolDefinition(string FilePath, int Line, int Column);

public sealed record TypeInfoResult(
    string TypeName,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> Members);

public sealed record TextSearchResult(string FilePath, int Line, string Snippet);

public sealed class RoslynToolServer
{
    private readonly IReadOnlyList<Compilation> _compilations;
    private readonly int _maxBudget;
    private int _callsMade;

    public RoslynToolServer(IReadOnlyList<Compilation> compilations, int maxBudget = 15)
    {
        _compilations = compilations;
        _maxBudget = maxBudget;
    }

    private void ConsumeBudget()
    {
        if (++_callsMade > _maxBudget)
            throw new InvalidOperationException($"Tool call budget ({_maxBudget} calls) exhausted.");
    }

    public Task<SymbolDefinition?> GoToDefinitionAsync(string symbolFqn, CancellationToken ct = default)
    {
        ConsumeBudget();
        foreach (var comp in _compilations)
        {
            var sym = FindSymbol(comp, symbolFqn);
            if (sym is not null)
            {
                var loc = sym.Locations.FirstOrDefault(l => l.IsInSource);
                if (loc is not null && loc.SourceTree is not null)
                {
                    var lineSpan = loc.GetLineSpan();
                    return Task.FromResult<SymbolDefinition?>(new SymbolDefinition(
                        loc.SourceTree.FilePath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1));
                }
            }
        }
        return Task.FromResult<SymbolDefinition?>(null);
    }

    public Task<TypeInfoResult?> GetTypeInfoAsync(string typeFqn, CancellationToken ct = default)
    {
        ConsumeBudget();
        foreach (var comp in _compilations)
        {
            var type = comp.GetTypeByMetadataName(typeFqn);
            if (type is not null)
            {
                var ifaces = type.AllInterfaces.Select(i => i.ToDisplayString()).ToList();
                var members = type.GetMembers().Select(m => m.ToDisplayString()).ToList();
                return Task.FromResult<TypeInfoResult?>(new TypeInfoResult(type.ToDisplayString(), ifaces, members));
            }
        }
        return Task.FromResult<TypeInfoResult?>(null);
    }

    public Task<IReadOnlyList<TextSearchResult>> SearchTextAsync(string query, int maxResults = 10, CancellationToken ct = default)
    {
        ConsumeBudget();
        var results = new List<TextSearchResult>();
        foreach (var comp in _compilations)
        {
            foreach (var tree in comp.SyntaxTrees)
            {
                var text = tree.GetText(ct);
                for (int i = 0; i < text.Lines.Count; i++)
                {
                    var lineStr = text.Lines[i].ToString();
                    if (lineStr.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new TextSearchResult(tree.FilePath, i + 1, lineStr.Trim()));
                        if (results.Count >= maxResults) return Task.FromResult<IReadOnlyList<TextSearchResult>>(results);
                    }
                }
            }
        }
        return Task.FromResult<IReadOnlyList<TextSearchResult>>(results);
    }

    private static ISymbol? FindSymbol(Compilation comp, string fqn)
    {
        var type = comp.GetTypeByMetadataName(fqn);
        if (type is not null) return type;

        var dot = fqn.LastIndexOf('.');
        if (dot > 0)
        {
            var parentTypeFqn = fqn.Substring(0, dot);
            var memberName = fqn.Substring(dot + 1);
            var parent = comp.GetTypeByMetadataName(parentTypeFqn);
            if (parent is not null)
            {
                return parent.GetMembers(memberName).FirstOrDefault();
            }
        }
        return null;
    }
}
