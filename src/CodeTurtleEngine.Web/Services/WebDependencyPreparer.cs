using System.Diagnostics;

namespace CodeTurtleEngine.Web.Services;

public sealed class WebDependencyOptions
{
    public bool Enabled { get; set; }
    public string[] OfflineSources { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 45;
}

public interface IWebDependencyPreparer
{
    Task PrepareAsync(string projectPath, CancellationToken ct);
}

// This is an explicit trusted-local capability, never a hosted acquisition fallback.
public sealed class WebDependencyPreparer(HostedAccessOptions access, WebDependencyOptions options) : IWebDependencyPreparer
{
    public async Task PrepareAsync(string projectPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (access.Mode != "TrustedLocal" || !options.Enabled)
            throw new InvalidOperationException("Trusted-local dependency preparation must be explicitly enabled.");
        if (options.TimeoutSeconds is < 1 or > 60 || options.OfflineSources.Length == 0 ||
            options.OfflineSources.Any(source => !Path.IsPathFullyQualified(source) || !Directory.Exists(source)))
            throw new InvalidOperationException("Configure existing absolute offline package directories and a 1–60 second deadline.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            CreateNoWindow = true
        };
        // --source overrides RestoreSources but NuGet still appends project-added sources.
        // A command-line global property prevents normal project/props assignments adding feeds.
        foreach (var arg in new[] { "restore", Path.GetFullPath(projectPath), "--disable-parallel", "--verbosity", "quiet", "-p:NuGetAudit=false", "-p:RestoreIgnoreFailedSources=false", "-p:RestoreAdditionalProjectSources=" })
            start.ArgumentList.Add(arg);
        foreach (var source in options.OfflineSources)
        {
            start.ArgumentList.Add("--source");
            start.ArgumentList.Add(Path.GetFullPath(source));
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Dependency preparation could not start.");
        // Drain in fixed-size buffers. Source-controlled diagnostics never enter API responses or logs.
        var stdout = DrainAsync(process.StandardOutput, deadline.Token);
        var stderr = DrainAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Dependency preparation timed out.");
        }
        if (process.ExitCode != 0) throw new InvalidOperationException("Dependency preparation failed.");
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false) > 0) { }
    }
}
