namespace CodeTurtleEngine.Web.Services;

public interface IRepoCloner : IDisposable
{
    Task<string> CloneAsync(string cloneUrl, string headBranch, string? targetDir = null, CancellationToken ct = default);
    void Cleanup(string directory);
}
