using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record LoadedCompilation(Compilation Compilation, IDisposable Workspace)
{
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public IReadOnlyList<Compilation> Projects { get; init; } = new[] { Compilation };
}

public interface ICompilationLoader
{
    Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default);
}

public sealed class MsBuildCompilationLoader : ICompilationLoader
{
    public async Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
    {
        var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();
        var diagnostics = new List<string>();
        using var handler = workspace.RegisterWorkspaceFailedHandler(e =>
        {
            lock (failures)
            {
                if (diagnostics.Count < 100) diagnostics.Add(e.Diagnostic.ToString());
                if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) failures.Add(e.Diagnostic.Message);
            }
        });
        try
        {
            IEnumerable<Project> projects;
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                projects = (await workspace.OpenSolutionAsync(path, cancellationToken: ct).ConfigureAwait(false)).Projects;
            else
            {
                await workspace.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);
                projects = workspace.CurrentSolution.Projects;
            }
            var compilations = new List<Compilation>();
            foreach (var project in projects.Where(p => p.Language == LanguageNames.CSharp))
            {
                var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                    ?? throw new CompilationException($"Compilation was null for '{project.Name}'.");
                var allDiagnostics = compilation.GetDiagnostics(ct);
                lock (failures)
                    diagnostics.AddRange(allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Take(Math.Max(0, 100 - diagnostics.Count)).Select(d => $"{project.Name}: {d}"));
                var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (errors.Count > 0)
                    throw new CompilationException($"Incomplete project '{project.Name}': {errors.Count} compilation error(s). " +
                        string.Join("\n", errors.Take(20)));
                compilations.Add(compilation);
            }
            lock (failures)
            {
                if (failures.Count > 0)
                    throw new CompilationException("Workspace loading failed: " + string.Join("\n", failures));
                if (compilations.Count == 0) throw new CompilationException($"No C# projects in '{path}'.");
                // Keep each project's parse options, references and binding context intact.
                return new LoadedCompilation(compilations[0], workspace) { Projects = compilations, Diagnostics = diagnostics.ToArray() };
            }
        }
        catch (Exception ex) when (ex is not CompilationException && ex is not OperationCanceledException)
        {
            workspace.Dispose();
            throw new CompilationException($"Failed to load compilation from '{path}': {ex.Message}", ex);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }
}
