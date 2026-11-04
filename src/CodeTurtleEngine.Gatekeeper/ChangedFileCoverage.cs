using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

internal static class ChangedFileCoverage
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static void Validate(string root, IReadOnlyList<string> changedPaths, IReadOnlyList<Compilation> compilations)
    {
        if (!Directory.Exists(root)) throw new CompilationException("Repository root is unavailable for changed-file coverage.");
        var missing = new List<string>();
        foreach (var relative in changedPaths)
        {
            var path = SourcePath(root, relative);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { continue; } // Deleted source has no head-side tree to review.
            catch (DirectoryNotFoundException) { continue; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new CompilationException($"Cannot inspect changed source '{relative}'.", ex);
            }
            if ((attributes & FileAttributes.Directory) != 0)
                throw new CompilationException($"Changed source '{relative}' is a directory.");
            if (!compilations.SelectMany(c => c.SyntaxTrees).Any(t => SamePath(t.FilePath, relative, root)))
                missing.Add(relative);
        }
        if (missing.Count > 0)
            throw new CompilationException("Changed C# files outside the loaded compilation: " +
                string.Join(", ", missing.Take(20)) + ". Select a solution or project that includes all changed source files.");
    }

    public static bool SamePath(string treePath, string relative, string root) =>
        !string.IsNullOrEmpty(treePath) && Path.GetFullPath(treePath, Path.GetFullPath(root))
            .Equals(SourcePath(root, relative), PathComparison);

    private static string SourcePath(string root, string relative)
    {
        var normalized = relative.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(normalized) || normalized.Contains(':') ||
            normalized.Split('/').Any(segment => segment is ".." or "."))
            throw new CompilationException("Unsafe changed source path.");
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(fullRoot, normalized));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison))
            throw new CompilationException("Changed source path escapes the repository.");
        return path;
    }
}
