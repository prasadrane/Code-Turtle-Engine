using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace CodeTurtleEngine.Cli.Bench;

public sealed record PreparedEntry(
    string RepoDir,
    string ProjectPath,
    string BaselineSha,
    string HeadSha,
    IReadOnlyList<ExpectedDefect>? Expected);

public sealed partial class CorpusPreparer
{
    private static readonly string[] SkippedDirs = { "bin", "obj", ".git" };
    private static readonly Signature Bot = new("turtle-bench", "bench@localhost", DateTimeOffset.UnixEpoch);

    [GeneratedRegex(@"\s*//\s*BENCH:\s*(?<cls>[A-Za-z]+)\s*$")]
    private static partial Regex Marker();

    private readonly string _workRoot;
    private readonly Func<string, CancellationToken, Task> _restore;

    public CorpusPreparer(string workRoot, Func<string, CancellationToken, Task> restore)
    {
        _workRoot = workRoot;
        _restore = restore;
    }

    public async Task<PreparedEntry> PrepareAsync(CorpusEntry entry, CancellationToken ct)
    {
        var dir = Path.Combine(_workRoot, entry.Id);
        var prepared = entry.Kind == CorpusKind.Seeded ? PrepareSeeded(entry, dir) : PrepareOss(entry, dir);
        await _restore(prepared.ProjectPath, ct).ConfigureAwait(false);
        return prepared;
    }

    private static PreparedEntry PrepareSeeded(CorpusEntry e, string dir)
    {
        DeleteDirectory(dir);
        Directory.CreateDirectory(dir);
        Repository.Init(dir);
        using var repo = new Repository(dir);

        CopyTree(e.BaseDir!, dir);
        Commands.Stage(repo, "*");
        var baseCommit = repo.Commit("base", Bot, Bot);

        CopyTree(e.HeadDir!, dir);
        var expected = ExtractMarkers(e.HeadDir!, dir);
        Commands.Stage(repo, "*");
        Commit head;
        try { head = repo.Commit("head", Bot, Bot); }
        catch (EmptyCommitException) { throw new BenchException($"{e.Id}: head overlay does not change the base."); }

        return new PreparedEntry(dir, Path.Combine(dir, e.Project), baseCommit.Sha, head.Sha, expected);
    }

    private static PreparedEntry PrepareOss(CorpusEntry e, string dir)
    {
        if (!Repository.IsValid(dir))
        {
            DeleteDirectory(dir);
            Repository.Clone(e.CloneUrl!, dir);
        }
        using var repo = new Repository(dir);
        _ = repo.Lookup<Commit>(e.BaseSha!) ?? throw new BenchException($"{e.Id}: baseSha {e.BaseSha} not found in clone.");
        var head = repo.Lookup<Commit>(e.HeadSha!) ?? throw new BenchException($"{e.Id}: headSha {e.HeadSha} not found in clone.");
        Commands.Checkout(repo, head, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        return new PreparedEntry(dir, Path.Combine(dir, e.Project), e.BaseSha!, head.Sha, e.Expected);
    }

    /// <summary>Records and strips trailing "// BENCH: Class" markers in the head overlay's .cs files.</summary>
    private static IReadOnlyList<ExpectedDefect> ExtractMarkers(string headDir, string repoDir)
    {
        var expected = new List<ExpectedDefect>();
        foreach (var src in Directory.EnumerateFiles(headDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(headDir, src).Replace('\\', '/');
            var target = Path.Combine(repoDir, rel);
            var lines = File.ReadAllText(target).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var m = Marker().Match(lines[i].TrimEnd('\r'));
                if (!m.Success) continue;
                expected.Add(new ExpectedDefect(rel, i + 1, m.Groups["cls"].Value));
                lines[i] = lines[i].TrimEnd('\r')[..m.Index] + (lines[i].EndsWith('\r') ? "\r" : "");
            }
            File.WriteAllText(target, string.Join('\n', lines));
        }
        return expected;
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var src in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, src);
            var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (SkippedDirs.Contains(first, StringComparer.OrdinalIgnoreCase)) continue;
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, overwrite: true);
        }
    }

    // .git object files are read-only on Windows; clear attributes before deleting.
    private static void DeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, recursive: true);
    }
}
