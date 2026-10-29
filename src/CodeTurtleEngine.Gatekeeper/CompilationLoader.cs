using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record LoadedCompilation(Compilation Compilation, IDisposable Workspace)
{
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public IReadOnlyList<Compilation> Projects { get; init; } = new[] { Compilation };
    public IReadOnlyList<string> DegradedProjects { get; init; } = Array.Empty<string>();
}

public interface ICompilationLoader
{
    Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default);
    Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, IReadOnlyList<string>? changedFiles, CancellationToken ct = default)
        => LoadAsync(projectOrSolutionPath, ct);
}

public sealed class MsBuildCompilationLoader : ICompilationLoader
{
    public Task<LoadedCompilation> LoadAsync(string path, CancellationToken ct = default)
        => LoadAsync(path, null, ct);

    public async Task<LoadedCompilation> LoadAsync(string path, IReadOnlyList<string>? changedFiles, CancellationToken ct = default)
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
            var degradedProjects = new List<string>();

            bool IsProjectChanged(Project project)
            {
                if (changedFiles is null || changedFiles.Count == 0) return true;
                return project.Documents.Any(d =>
                    changedFiles.Any(cf =>
                        !string.IsNullOrEmpty(d.FilePath) &&
                        (d.FilePath.EndsWith(cf, StringComparison.OrdinalIgnoreCase) ||
                         d.FilePath.EndsWith(cf.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase) ||
                         d.FilePath.EndsWith(cf.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))));
            }

            foreach (var project in projects.Where(p => p.Language == LanguageNames.CSharp))
            {
                var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
                if (compilation is null)
                {
                    if (IsProjectChanged(project))
                        throw new CompilationException($"Compilation was null for '{project.Name}'.");
                    degradedProjects.Add(project.Name);
                    continue;
                }

                var allDiagnostics = compilation.GetDiagnostics(ct);
                lock (failures)
                    diagnostics.AddRange(allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Take(Math.Max(0, 100 - diagnostics.Count)).Select(d => $"{project.Name}: {d}"));
                var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (errors.Count > 0)
                {
                    if (IsProjectChanged(project))
                        throw new CompilationException($"Incomplete project '{project.Name}': {errors.Count} compilation error(s). " +
                            string.Join("\n", errors.Take(20)));

                    degradedProjects.Add(project.Name);
                    lock (failures)
                    {
                        if (diagnostics.Count < 100)
                            diagnostics.Add($"Degraded project '{project.Name}': {errors.Count} compilation error(s). {errors[0]}");
                    }
                    continue;
                }
                compilations.Add(compilation);
            }
            lock (failures)
            {
                if (compilations.Count == 0)
                {
                    if (failures.Count > 0)
                        throw new CompilationException("Workspace loading failed: " + string.Join("\n", failures));
                    throw new CompilationException($"No C# projects compiled successfully in '{path}'.");
                }
                if (failures.Count > 0)
                {
                    diagnostics.AddRange(failures.Take(Math.Max(0, 100 - diagnostics.Count)).Select(f => $"Workspace diagnostic: {f}"));
                }
                // Keep each project's parse options, references and binding context intact.
                return new LoadedCompilation(compilations[0], workspace)
                {
                    Projects = compilations,
                    Diagnostics = diagnostics.ToArray(),
                    DegradedProjects = degradedProjects.ToArray()
                };
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
