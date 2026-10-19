namespace CodeTurtleEngine.Web.Services;

public interface IRepoCloner : IDisposable
{
    Task<string> CloneAsync(string cloneUrl, string headBranch, string? targetDir = null, CancellationToken ct = default);
    void Cleanup(string directory);
    Task PrepareComparisonAsync(string directory, string cloneUrl, string baseSha, string headSha,
        CancellationToken ct = default) => Task.CompletedTask;
}
