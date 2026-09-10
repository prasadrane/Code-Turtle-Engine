using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Web.Services;

/// <summary>
/// Provides changed C# files from a GitHub Pull Request file list,
/// implementing <see cref="IDiffProvider"/> without requiring local git diffing.
/// </summary>
public sealed class GitHubPrDiffProvider : IDiffProvider
{
    private readonly IReadOnlyList<string> _changedCsFiles;

    public GitHubPrDiffProvider(IEnumerable<string>? changedFilePaths)
    {
        if (changedFilePaths is null)
        {
            _changedCsFiles = Array.Empty<string>();
            return;
        }

        var normalizedList = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in changedFilePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var normalized = rawPath.Trim().Replace('\\', '/');

            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized[2..];
            }

            normalized = normalized.TrimStart('/');

            if (!normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                normalizedList.Add(normalized);
            }
        }

        _changedCsFiles = normalizedList.AsReadOnly();
    }

    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef)
        => _changedCsFiles;
}
