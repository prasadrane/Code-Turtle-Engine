namespace CodeTurtleEngine.Gatekeeper.Tests;

public static class TestPaths
{
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodeTurtleEngine.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repo root (sln) not found.");
        }
    }

    public static string SampleRepoCsproj =>
        Path.Combine(RepoRoot, "tests", "fixtures", "SampleRepo", "SampleRepo.csproj");
}
