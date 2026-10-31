using System.Text.RegularExpressions;
using CodeTurtleEngine.Core;
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper;

public interface IDiffProvider
{
    IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef);
    /// <summary>
    /// Changed head-side line runs. <c>null</c> means no hunk evidence is available (distinct from an
    /// empty list, which means the diff exists but adds no lines).
    /// </summary>
    IReadOnlyList<DiffHunk>? GetDiffHunks(string repoPath, string? baselineRef) => null;
}

public sealed class GitDiffProvider : IDiffProvider
{
    private static readonly Regex HunkHeaderRegex = new(
        @"^@@\s+-(?:\d+)(?:,\d+)?\s+\+(\d+)(?:,(\d+))?\s+@@",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static Commit? FindMergeBase(Repository repo, Commit? baseline, Commit? head)
    {
        if (baseline is null || head is null) return null;
        try
        {
            return repo.ObjectDatabase.FindMergeBase(baseline, head);
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
    }

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

    public IReadOnlyList<DiffHunk>? GetDiffHunks(string repoPath, string? baselineRef) =>
        GetDiffHunks(repoPath, baselineRef, useMergeBase: false);

    public IReadOnlyList<DiffHunk>? GetDiffHunks(string repoPath, string? baselineRef, bool useMergeBase)
    {
        try
        {
            using var repo = new Repository(repoPath);
            Patch patch;
            if (baselineRef is null)
            {
                patch = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory);
            }
            else if (useMergeBase)
            {
                var branch = repo.Branches[baselineRef];
                var baselineCommit = branch?.Tip ?? repo.Lookup<Commit>(baselineRef);
                if (baselineCommit is null) return null;

                var headCommit = repo.Head?.Tip;
                if (headCommit is null) return null;

                var mergeBase = FindMergeBase(repo, baselineCommit, headCommit);
                if (mergeBase is null) return null;

                patch = repo.Diff.Compare<Patch>(mergeBase.Tree, headCommit.Tree);
            }
            else
            {
                patch = ComparePatch(repo, baselineRef);
            }

            var list = new List<DiffHunk>();
            foreach (var entry in patch)
            {
                if (!entry.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                list.AddRange(ExtractHunksFromPatch(entry.Path, entry.Patch));
            }
            return list;
        }
        catch (Exception ex) when (ex is not DiffException)
        {
            throw new DiffException($"Diff failed for '{repoPath}': {ex.Message}", ex);
        }
    }

    public static IReadOnlyList<DiffHunk> ExtractHunksFromPatch(string filePath, string patchText)
    {
        var hunks = new List<DiffHunk>();
        if (string.IsNullOrEmpty(patchText)) return hunks;

        int headLine = 0;
        int? currentRunStart = null;
        int currentRunCount = 0;
        bool inHunk = false;

        void FlushCurrentRun()
        {
            if (currentRunStart.HasValue && currentRunCount > 0)
            {
                hunks.Add(new DiffHunk(filePath, currentRunStart.Value, currentRunCount));
                currentRunStart = null;
                currentRunCount = 0;
            }
        }

        foreach (var rawLine in patchText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                FlushCurrentRun();
                var match = HunkHeaderRegex.Match(line);
                if (match.Success)
                {
                    headLine = int.Parse(match.Groups[1].Value);
                    inHunk = true;
                }
                else
                {
                    inHunk = false;
                }
                continue;
            }

            if (!inHunk || line.Length == 0) continue;

            if (line[0] == '+')
            {
                if (!currentRunStart.HasValue)
                {
                    currentRunStart = headLine;
                    currentRunCount = 1;
                }
                else
                {
                    currentRunCount++;
                }
                headLine++;
            }
            else if (line[0] == '-')
            {
                // Deleted line: only in old tree, head line does not advance
            }
            else if (line[0] == ' ')
            {
                FlushCurrentRun();
                headLine++;
            }
            else
            {
                FlushCurrentRun();
            }
        }

        FlushCurrentRun();
        return hunks;
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
