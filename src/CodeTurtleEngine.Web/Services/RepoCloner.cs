using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodeTurtleEngine.Web.Services;

// Explicit trusted-local legacy path only. Hosted acquisition stays disabled.
public sealed class RepoCloner(string? baseDirectory = null) : IRepoCloner
{
    private readonly ConcurrentBag<string> _tracked = new();
    public async Task<string> CloneAsync(string cloneUrl, string headBranch, string? targetDir = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneUrl);
        if (!Regex.IsMatch(cloneUrl, @"\Ahttps://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\.git)?\z") ||
            !Regex.IsMatch(headBranch, @"\A[0-9a-fA-F]{40}\z"))
            throw new ArgumentException("A GitHub HTTPS repository and exact commit SHA are required.");
        var destination = targetDir ?? Path.Combine(baseDirectory ?? Path.GetTempPath(), "codeturtle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        _tracked.Add(destination);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await Git(destination, ["init", "--quiet"], deadline.Token);
            await Git(destination, ["fetch", "--no-tags", "--depth=1", "--", cloneUrl, headBranch], deadline.Token);
            await Git(destination, ["checkout", "--detach", "--quiet", "FETCH_HEAD"], deadline.Token);
            var actual = await Git(destination, ["rev-parse", "HEAD"], deadline.Token);
            if (!string.Equals(actual.Trim(), headBranch, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Checkout identity mismatch.");
            return destination;
        }
        catch { Cleanup(destination); throw; }
    }

    private static async Task<string> Git(string working, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = working,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-c", "core.hooksPath=/dev/null", "-c", "http.followRedirects=false" }.Concat(args))
            start.ArgumentList.Add(arg);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Clone could not start.");
        var stdout = ReadBounded(process.StandardOutput);
        var stderr = ReadBounded(process.StandardError);
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        await stderr;
        var text = await stdout;
        if (process.ExitCode != 0) throw new InvalidOperationException("Repository acquisition failed.");
        return text;
    }

    private static async Task<string> ReadBounded(StreamReader reader)
    {
        var buffer = new char[4096];
        var result = new System.Text.StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            if (result.Length < 4096) result.Append(buffer, 0, Math.Min(count, 4096 - result.Length));
        return result.ToString();
    }

    public void Cleanup(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        try { Delete(new DirectoryInfo(directory)); }
        catch { Console.Error.WriteLine("Clone cleanup failed; operator intervention required."); }
    }
    private static void Delete(DirectoryInfo directory)
    {
        if (directory.LinkTarget is not null) { directory.Delete(); return; }
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo child) Delete(child);
            else
            {
                if (entry.LinkTarget is null) entry.Attributes = FileAttributes.Normal;
                entry.Delete();
            }
        }
        directory.Delete();
    }
    public void Dispose() { foreach (var path in _tracked) Cleanup(path); }
}
