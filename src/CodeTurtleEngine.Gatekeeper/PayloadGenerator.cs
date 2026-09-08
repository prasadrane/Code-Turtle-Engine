using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class PayloadGenerator
{
    private readonly Compilation _compilation;
    private readonly SemanticAnalyzer _analyzer;

    public PayloadGenerator(Compilation compilation)
    {
        _compilation = compilation;
        _analyzer = new SemanticAnalyzer(compilation);
    }

    public RoslynPayload Build(string repoSlug, string diffBaseline, IReadOnlyList<string> changedRelativePaths)
    {
        var files = new List<FileFacts>();
        foreach (var rel in changedRelativePaths)
        {
            var tree = _compilation.SyntaxTrees.FirstOrDefault(t => PathsMatch(t.FilePath, rel));
            if (tree is null) continue;

            var methods = _analyzer.AnalyzeTree(tree);
            if (methods.Count == 0) continue;

            files.Add(new FileFacts(rel, methods));
        }
        return new RoslynPayload(repoSlug, diffBaseline, files);
    }

    private static bool PathsMatch(string treePath, string rel)
    {
        var a = treePath.Replace('\\', '/');
        var b = rel.Replace('\\', '/');
        return a.EndsWith(b, StringComparison.OrdinalIgnoreCase);
    }
}
