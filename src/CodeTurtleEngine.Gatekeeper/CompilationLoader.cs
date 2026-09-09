using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record LoadedCompilation(Compilation Compilation, IDisposable Workspace);

public interface ICompilationLoader
{
    Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default);
}

public sealed class MsBuildCompilationLoader : ICompilationLoader
{
    public async Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
    {
        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        try
        {
            Project project = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                ? (await workspace.OpenSolutionAsync(path, cancellationToken: ct).ConfigureAwait(false)).Projects.FirstOrDefault()
                    ?? throw new CompilationException($"No projects in solution '{path}'.")
                : await workspace.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);

            var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                ?? throw new CompilationException($"Compilation was null for '{path}'.");

            return new LoadedCompilation(compilation, workspace);
        }
        catch (Exception ex) when (ex is not CompilationException)
        {
            workspace.Dispose();
            throw new CompilationException($"Failed to load compilation from '{path}': {ex.Message}", ex);
        }
    }
}
