using System.Diagnostics;

namespace CodeTurtleEngine.Cli.Bench;

// Explicit benchmark preparation only. Never invoke for product review / untrusted clones.
public static class DotnetRestore
{
    public static Task RunAsync(string projectPath, CancellationToken ct) =>
        RunAsync(projectPath, TimeSpan.FromMinutes(2), ct);

    public static async Task RunAsync(string projectPath, TimeSpan timeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("restore");
        psi.ArgumentList.Add(projectPath);
        psi.ArgumentList.Add("--disable-parallel");
        using var process = Process.Start(psi) ?? throw new BenchException("Failed to start 'dotnet restore'.");
        // Drain both streams concurrently, retaining only a bounded tail.
        var stdout = ReadTailAsync(process.StandardOutput);
        var stderr = ReadTailAsync(process.StandardError);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            throw new BenchException($"Trusted restore exceeded {timeout} for '{projectPath}'.");
        }
        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new BenchException($"dotnet restore failed for '{projectPath}' (exit {process.ExitCode}):\n{output}\n{error}");
    }

    private static async Task<string> ReadTailAsync(StreamReader reader)
    {
        var tail = new Queue<string>();
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            tail.Enqueue(line.Length > 2000 ? line[..2000] : line);
            if (tail.Count > 10) tail.Dequeue();
        }
        return string.Join('\n', tail);
    }
}
