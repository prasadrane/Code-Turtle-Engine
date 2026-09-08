using CodeTurtleEngine.Gatekeeper;
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class DiffProviderTests
{
    [Fact]
    public void Returns_Changed_Cs_Files_WorkingTree_Vs_Head()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A {}\n");
                File.WriteAllText(Path.Combine(dir, "B.txt"), "ignore\n");
                Commands.Stage(repo, "A.cs");
                Commands.Stage(repo, "B.txt");
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                repo.Commit("init", sig, sig);

                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A { int x; }\n");

                var changed = new GitDiffProvider().GetChangedCSharpFiles(dir, null);

                Assert.Contains("A.cs", changed);
                Assert.DoesNotContain("B.txt", changed);
            }
        }
        finally
        {
            ClearAttributes(dir);
            Directory.Delete(dir, true);
        }
    }

    private static void ClearAttributes(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
    }
}
