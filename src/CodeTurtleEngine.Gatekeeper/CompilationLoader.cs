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
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var solution = await workspace.OpenSolutionAsync(path, cancellationToken: ct).ConfigureAwait(false);
                var csharpProjects = solution.Projects
                    .Where(p => p.Language == LanguageNames.CSharp)
                    .ToList();

                if (csharpProjects.Count == 0)
                    throw new CompilationException($"No C# projects in solution '{path}'.");

                var compilations = new List<Compilation>();
                foreach (var project in csharpProjects)
                {
                    var comp = await project.GetCompilationAsync(ct).ConfigureAwait(false);
                    if (comp is not null) compilations.Add(comp);
                }

                if (compilations.Count == 0)
                    throw new CompilationException($"Failed to load compilations for solution '{path}'.");

                var merged = compilations[0];
                for (int i = 1; i < compilations.Count; i++)
                {
                    var sec = compilations[i];
                    var treesToAdd = sec.SyntaxTrees.Where(t => !IsGeneratedAttributeTree(t.FilePath));
                    merged = merged.AddSyntaxTrees(treesToAdd);

                    var existingRefs = new HashSet<MetadataReference>(merged.References);
                    var refsToAdd = sec.References.Where(r => !existingRefs.Contains(r));
                    merged = merged.AddReferences(refsToAdd);
                }

                return new LoadedCompilation(merged, workspace);
            }
            else
            {
                var project = await workspace.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);
                var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                    ?? throw new CompilationException($"Compilation was null for '{path}'.");
                return new LoadedCompilation(compilation, workspace);
            }
        }
        catch (Exception ex) when (ex is not CompilationException && ex is not OperationCanceledException)
        {
            workspace.Dispose();
            throw new CompilationException($"Failed to load compilation from '{path}': {ex.Message}", ex);
        }
        catch
        {
            // OCE and CompilationException propagate unwrapped; workspace is not owned by anyone yet.
            workspace.Dispose();
            throw;
        }
    }

    private static bool IsGeneratedAttributeTree(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var fileName = Path.GetFileName(filePath);
        return fileName.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase);
    }
}
