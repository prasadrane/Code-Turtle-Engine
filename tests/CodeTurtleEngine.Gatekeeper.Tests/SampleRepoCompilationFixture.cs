using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public sealed class SampleRepoCompilationFixture : IAsyncLifetime
{
    public LoadedCompilation Loaded { get; private set; } = default!;

    public async Task InitializeAsync()
        => Loaded = await new MsBuildCompilationLoader().LoadAsync(TestPaths.SampleRepoCsproj);

    public Task DisposeAsync()
    {
        Loaded.Workspace?.Dispose();
        return Task.CompletedTask;
    }
}
