using System.Collections.Concurrent;
using System.Diagnostics;

namespace CodeTurtleEngine.Web.Services;

public sealed class RepoCloner : IRepoCloner
{
    private readonly ConcurrentBag<string> _trackedDirectories = new();
    private readonly string? _baseDirectory;

    public RepoCloner(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory;
    }

    public async Task<string> CloneAsync(string cloneUrl, string headBranch, string? targetDir = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneUrl);

        var destination = targetDir ?? Path.Combine(_baseDirectory ?? Path.GetTempPath(), "codeturtle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        _trackedDirectories.Add(destination);

        var branchArg = string.IsNullOrWhiteSpace(headBranch) ? string.Empty : $"--branch \"{headBranch}\" ";
        var arguments = $"clone --depth 1 {branchArg}\"{cloneUrl}\" \"{destination}\"";

        var psi = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            Cleanup(destination);
            throw new InvalidOperationException($"git clone failed with exit code {process.ExitCode}: {stderr}");
        }

        return destination;
    }

    public void Cleanup(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            ForceDeleteDirectory(directory);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RepoCloner] Cleanup failed for '{directory}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var dir in _trackedDirectories)
        {
            Cleanup(dir);
        }
    }

    private static void ForceDeleteDirectory(string targetDir)
    {
        foreach (var file in Directory.GetFiles(targetDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(targetDir))
        {
            ForceDeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }
}
