using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class PayloadGenerator
{
    private readonly IReadOnlyList<Compilation> _compilations;

    public PayloadGenerator(Compilation compilation) : this(new[] { compilation }) { }

    public PayloadGenerator(IReadOnlyList<Compilation> compilations) => _compilations = compilations;

    public RoslynPayload Build(string repoSlug, string diffBaseline, IReadOnlyList<string> changedRelativePaths,
        IReadOnlyList<DiffHunk>? diffHunks = null, string? repositoryRoot = null)
    {
        if (repositoryRoot is not null)
            ChangedFileCoverage.Validate(repositoryRoot, changedRelativePaths, _compilations);
        var files = new List<FileFacts>();
        foreach (var rel in changedRelativePaths)
        {
            var fileHunks = diffHunks?.Where(h =>
                RepoPaths.EndsWith(rel, h.FilePath) || RepoPaths.EndsWith(h.FilePath, rel)).ToList();
            foreach (var compilation in _compilations)
            {
                var tree = compilation.SyntaxTrees.FirstOrDefault(t => repositoryRoot is null
                    ? RepoPaths.EndsWith(t.FilePath, rel)
                    : ChangedFileCoverage.SamePath(t.FilePath, rel, repositoryRoot));
                if (tree is null) continue;
                var methods = new SemanticAnalyzer(compilation).AnalyzeTree(tree);
                if (methods.Count > 0) files.Add(new FileFacts(rel, methods, fileHunks));
            }
        }
        return new RoslynPayload(repoSlug, diffBaseline, files);
    }
}
