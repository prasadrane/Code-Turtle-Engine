using System.Text.RegularExpressions;
using CodeTurtleEngine.Core;
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper;

public interface IDiffProvider
{
    IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef);
    IReadOnlyList<DiffHunk> GetDiffHunks(string repoPath, string? baselineRef) => Array.Empty<DiffHunk>();
}

public sealed class GitDiffProvider : IDiffProvider
{
    private static readonly Regex HunkHeaderRegex = new(
        @"^@@\s+-(?:\d+)(?:,\d+)?\s+\+(\d+)(?:,(\d+))?\s+@@",
        RegexOptions.Compiled | RegexOptions.Multiline);

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

    public IReadOnlyList<DiffHunk> GetDiffHunks(string repoPath, string? baselineRef)
    {
        try
        {
            using var repo = new Repository(repoPath);
            Patch patch = baselineRef is null
                ? repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory)
                : ComparePatch(repo, baselineRef);

            var list = new List<DiffHunk>();
            foreach (var entry in patch)
            {
                if (!entry.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                var matches = HunkHeaderRegex.Matches(entry.Patch);
                foreach (Match m in matches)
                {
                    var startLine = int.Parse(m.Groups[1].Value);
                    var lineCount = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
                    list.Add(new DiffHunk(entry.Path, startLine, lineCount));
                }
            }
            return list;
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

    private static Patch ComparePatch(Repository repo, string baselineRef)
    {
        var branch = repo.Branches[baselineRef];
        var baselineCommit = branch?.Tip ?? repo.Lookup<Commit>(baselineRef)
            ?? throw new DiffException($"Baseline '{baselineRef}' not found.");
        return repo.Diff.Compare<Patch>(baselineCommit.Tree, repo.Head.Tip.Tree);
    }
}
