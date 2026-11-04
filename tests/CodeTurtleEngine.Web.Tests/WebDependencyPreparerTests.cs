using System.Text.Json;
using CodeTurtleEngine.Web.Services;

namespace CodeTurtleEngine.Web.Tests;

public class WebDependencyPreparerTests
{
    [Theory]
    [InlineData("Disabled", true)]
    [InlineData("Hosted", true)]
    [InlineData("TrustedLocal", false)]
    public async Task DoesNotPrepareWithoutExplicitTrustedLocalCapability(string mode, bool enabled)
    {
        var preparer = new WebDependencyPreparer(new() { Mode = mode }, new() { Enabled = enabled });
        await Assert.ThrowsAsync<InvalidOperationException>(() => preparer.PrepareAsync("missing.csproj", CancellationToken.None));
    }

    [Theory]
    [InlineData("https://api.nuget.org/v3/index.json", 45)]
    [InlineData("relative-feed", 45)]
    [InlineData("", 0)]
    [InlineData("", 61)]
    public async Task RejectsRemoteOrRelativeFeedsAndInvalidDeadlines(string source, int timeout)
    {
        var preparer = new WebDependencyPreparer(new() { Mode = "TrustedLocal" }, new() {
            Enabled = true, OfflineSources = [source], TimeoutSeconds = timeout });
        await Assert.ThrowsAsync<InvalidOperationException>(() => preparer.PrepareAsync("missing.csproj", CancellationToken.None));
    }

    [Fact]
    public async Task CancelledPreparationDoesNotLaunchRestore()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WebDependencyPreparer(new(), new())
            .PrepareAsync("missing.csproj", cancellation.Token));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("project")]
    [InlineData("props")]
    public async Task RestoresFreshSdkProjectUsingOnlyApprovedSources(string additionalSourceLocation)
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-web-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var feed = Directory.CreateDirectory(Path.Combine(root, "feed")).FullName;
        var project = Path.Combine(root, "Fresh.csproj");
        const string additionalSource = "<RestoreAdditionalProjectSources>https://example.invalid/nuget/index.json</RestoreAdditionalProjectSources>";
        await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>" +
            (additionalSourceLocation == "project" ? additionalSource : "") + "</PropertyGroup></Project>");
        if (additionalSourceLocation == "props")
            await File.WriteAllTextAsync(Path.Combine(root, "Directory.Build.props"), "<Project><PropertyGroup>" + additionalSource + "</PropertyGroup></Project>");
        try
        {
            var preparer = new WebDependencyPreparer(new() { Mode = "TrustedLocal" }, new() {
                Enabled = true, OfflineSources = [feed], TimeoutSeconds = 30 });
            Assert.False(File.Exists(Path.Combine(root, "obj", "project.assets.json")));
            await preparer.PrepareAsync(project, CancellationToken.None);
            using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "obj", "project.assets.json")));
            var sources = assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("sources")
                .EnumerateObject().Select(source => Path.TrimEndingDirectorySeparator(source.Name)).ToArray();
            Assert.Equal([Path.TrimEndingDirectorySeparator(feed)], sources);
        }
        finally { Directory.Delete(root, true); }
    }
}
