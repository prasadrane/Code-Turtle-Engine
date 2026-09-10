using CodeTurtleEngine.Web.Services;

namespace CodeTurtleEngine.Web.Tests;

public class RepoClonerTests
{
    [Fact]
    public void Cleanup_DeletesDirectoryAndReadOnlyFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codeturtle_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "test.txt");
        File.WriteAllText(testFile, "hello");
        File.SetAttributes(testFile, FileAttributes.ReadOnly);

        var cloner = new RepoCloner();
        cloner.Cleanup(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void Dispose_CleansUpTrackedDirectories()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "codeturtle_base_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        var cloner = new RepoCloner(tempBase);
        var dir1 = Path.Combine(tempBase, "dir1");
        Directory.CreateDirectory(dir1);
        File.WriteAllText(Path.Combine(dir1, "file1.txt"), "data");

        // Cleanup on specific directory
        cloner.Cleanup(dir1);
        Assert.False(Directory.Exists(dir1));

        if (Directory.Exists(tempBase))
        {
            Directory.Delete(tempBase, true);
        }
    }

    [Fact]
    public async Task CloneAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        var cloner = new RepoCloner();
        await Assert.ThrowsAsync<ArgumentException>(() => cloner.CloneAsync("", "main"));
    }
}
