using System.Text;
using System.Text.RegularExpressions;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Web.Models;
using LibGit2Sharp;

namespace CodeTurtleEngine.Web.Services;

public sealed class ReviewChangesBuilder
{
    public const int MaxFileBytes = 64 * 1024;
    public const int MaxTotalBytes = 512 * 1024;
    public const int MaxFiles = 60;

    public ReviewChanges Build(string directory, string repository, int pullNumber,
        GitHubPrDetails details, RoslynPayload payload)
    {
        using var repo = new Repository(directory);
        var head = repo.Lookup<Commit>(details.HeadSha);
        if (head is null || repo.Head.Tip.Sha != details.HeadSha)
            throw new InvalidOperationException("Review snapshot identity mismatch.");
        var baseline = repo.Lookup<Commit>(details.BaseSha);
        Commit? mergeBase = null;
        try { if (baseline is not null) mergeBase = repo.ObjectDatabase.FindMergeBase(baseline, head); }
        catch (LibGit2SharpException) { /* Partial history is a display limitation, not a reason to fabricate a diff. */ }
        var snapshot = new ReviewSnapshot(repository, pullNumber, details.Title, details.BaseSha,
            details.HeadSha, mergeBase?.Sha, mergeBase is null ? "head-only" : "merge-base-to-head");
        var files = new List<ChangedFile>();
        var budget = MaxTotalBytes;
        var omitted = false;
        if (mergeBase is not null)
        {
            // Exact renames are deterministic and do not need unbounded similarity scanning.
            using var changed = repo.Diff.Compare<TreeChanges>(mergeBase.Tree, head.Tree,
                new CompareOptions { Similarity = SimilarityOptions.Exact });
            foreach (var item in changed.OrderBy(x => DisplayPriority(x.Path, payload)).ThenBy(x => x.Path, StringComparer.Ordinal))
            {
                if (files.Count >= MaxFiles) { omitted = true; break; }
                files.Add(BuildFile(repo, head, mergeBase, item.Path, item.OldPath, item.Status.ToString(), payload, ref budget));
            }
        }
        else
        {
            foreach (var path in details.ChangedFiles.Distinct(StringComparer.Ordinal).OrderBy(path => DisplayPriority(path, payload)).ThenBy(path => path, StringComparer.Ordinal).Take(MaxFiles))
                files.Add(BuildFile(repo, head, null, path, null, "Unknown", payload, ref budget));
            omitted = details.ChangedFiles.Count > MaxFiles;
        }
        var reason = mergeBase is null ? "Diff unavailable: the required base history could not be established. Showing the pinned head source only." : null;
        if (omitted) reason = (reason is null ? "" : reason + " ") + $"File display limited to {MaxFiles} files; additional files omitted.";
        return new ReviewChanges(1, snapshot, files, [], reason);
    }

    private static int DisplayPriority(string path, RoslynPayload payload) => payload.Files.Any(file => file.FilePath == path)
        ? 0 : path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

    private static ChangedFile BuildFile(Repository repo, Commit head, Commit? baseline, string path,
        string? oldPath, string status, RoslynPayload payload, ref int budget)
    {
        var analyzed = payload.Files.Any(f => f.FilePath == path);
        ChangedFile Omit(string reason) => new(path, oldPath, status, analyzed, null, [], reason);
        if (!SafePath(path) || oldPath is not null && !SafePath(oldPath)) return Omit("Invalid repository path.");
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return Omit("Outside the current C# source display scope; not analyzed.");
        var headEntry = head.Tree[path];
        var baseEntry = baseline?.Tree[oldPath ?? path];
        if ((headEntry is not null && headEntry.Mode is not (Mode.NonExecutableFile or Mode.ExecutableFile)) ||
            (baseEntry is not null && baseEntry.Mode is not (Mode.NonExecutableFile or Mode.ExecutableFile)))
            return Omit("Links and non-regular repository entries are not displayed.");
        var headBlob = headEntry?.Target as Blob;
        var baseBlob = baseEntry?.Target as Blob;
        var bytes = (headBlob?.Size ?? 0) + (baseBlob?.Size ?? 0);
        if ((headBlob?.Size ?? 0) > MaxFileBytes || (baseBlob?.Size ?? 0) > MaxFileBytes || bytes * 4 > budget)
            return Omit("Source omitted by file or review display size limit.");
        try
        {
            var source = ReadText(headBlob);
            _ = ReadText(baseBlob);
            IReadOnlyList<DiffHunk> hunks = [];
            if (baseline is not null)
            {
                var content = repo.Diff.Compare(baseBlob, headBlob, new CompareOptions { ContextLines = 3 });
                if (content.IsBinaryComparison) return Omit("Non-text content is not displayed.");
                hunks = ParseHunks(content.Patch);
            }
            budget -= checked((int)bytes * 4);
            return new ChangedFile(path, oldPath, status, analyzed, source, hunks,
                headBlob is null ? "No head source: this file was removed and was not analyzed." : null);
        }
        catch (DecoderFallbackException) { return Omit("Non-UTF-8 content is not displayed."); }
        catch (InvalidDataException) { return Omit("Non-text content is not displayed."); }
    }

    private static string? ReadText(Blob? blob)
    {
        if (blob is null) return null;
        using var stream = blob.GetContentStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        if (text.Contains('\0')) throw new InvalidDataException();
        return text;
    }

    private static bool SafePath(string path) => path.Length <= 512 && !path.StartsWith('/') &&
        !path.Contains('\\') && !path.Any(char.IsControl) && !path.Contains(':') &&
        path.Split('/').All(part => part is not ("" or "." or ".."));

    private static IReadOnlyList<DiffHunk> ParseHunks(string patch)
    {
        var result = new List<DiffHunk>();
        List<DiffLine>? lines = null;
        var oldLine = 0;
        var newLine = 0;
        foreach (var line in patch.Split('\n'))
        {
            var match = Regex.Match(line, @"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@");
            if (match.Success)
            {
                oldLine = int.Parse(match.Groups[1].Value);
                newLine = int.Parse(match.Groups[2].Value);
                lines = [];
                result.Add(new DiffHunk(line, lines));
            }
            else if (lines is not null && line.Length > 0)
            {
                if (line[0] == '+') lines.Add(new DiffLine("added", null, newLine++, line[1..].TrimEnd('\r')));
                else if (line[0] == '-') lines.Add(new DiffLine("removed", oldLine++, null, line[1..].TrimEnd('\r')));
                else if (line[0] == ' ') lines.Add(new DiffLine("context", oldLine++, newLine++, line[1..].TrimEnd('\r')));
            }
        }
        return result;
    }
}
