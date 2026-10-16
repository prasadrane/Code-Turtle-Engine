using System.Diagnostics;

namespace CodeTurtleEngine.Cli.Bench;

public static class DotnetRestore
{
    public static async Task RunAsync(string projectPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("restore");
        psi.ArgumentList.Add(projectPath);
        using var p = Process.Start(psi) ?? throw new BenchException("Failed to start 'dotnet restore'.");
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        if (p.ExitCode != 0)
        {
            var tail = string.Join('\n', ((await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false)))
                .Split('\n').TakeLast(10));
            throw new BenchException($"dotnet restore failed for '{projectPath}' (exit {p.ExitCode}):\n{tail}");
        }
    }
}
