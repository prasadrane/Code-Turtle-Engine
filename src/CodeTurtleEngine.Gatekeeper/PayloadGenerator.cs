using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class PayloadGenerator
{
    private readonly IReadOnlyList<Compilation> _compilations;

    public PayloadGenerator(Compilation compilation) : this(new[] { compilation }) { }

    public PayloadGenerator(IReadOnlyList<Compilation> compilations) => _compilations = compilations;

    public RoslynPayload Build(string repoSlug, string diffBaseline, IReadOnlyList<string> changedRelativePaths)
    {
        var files = new List<FileFacts>();
        foreach (var rel in changedRelativePaths)
        {
            foreach (var compilation in _compilations)
            {
                var tree = compilation.SyntaxTrees.FirstOrDefault(t => RepoPaths.EndsWith(t.FilePath, rel));
                if (tree is null) continue;
                var methods = new SemanticAnalyzer(compilation).AnalyzeTree(tree);
                if (methods.Count > 0) files.Add(new FileFacts(rel, methods));
            }
        }
        return new RoslynPayload(repoSlug, diffBaseline, files);
    }
}
