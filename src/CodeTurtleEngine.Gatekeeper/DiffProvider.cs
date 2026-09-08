using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper;

public interface IDiffProvider
{
    IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef);
}

public sealed class GitDiffProvider : IDiffProvider
{
    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef)
    {
        try
        {
            using var repo = new Repository(repoPath);
            TreeChanges changes = baselineRef is null
                ? repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory)
                : Compare(repo, baselineRef);

            return changes
                .Where(c => c.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Path)
                .Distinct()
                .ToList();
        }
        catch (Exception ex) when (ex is not DiffException)
        {
            throw new DiffException($"Diff failed for '{repoPath}': {ex.Message}", ex);
        }
    }

    private static TreeChanges Compare(Repository repo, string baselineRef)
    {
        var branch = repo.Branches[baselineRef];
        var baselineCommit = branch?.Tip ?? repo.Lookup<Commit>(baselineRef)
            ?? throw new DiffException($"Baseline '{baselineRef}' not found.");
        return repo.Diff.Compare<TreeChanges>(baselineCommit.Tree, repo.Head.Tip.Tree);
    }
}
