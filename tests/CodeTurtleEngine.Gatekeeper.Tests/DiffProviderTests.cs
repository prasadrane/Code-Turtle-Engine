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

                // Modify BOTH files so B.txt genuinely enters the working-tree
                // TreeChanges; DoesNotContain then proves the .cs filter, not absence.
                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A { int x; }\n");
                File.AppendAllText(Path.Combine(dir, "B.txt"), "changed\n");

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

    [Fact]
    public void Returns_Changed_Cs_Files_For_Baseline_Ref()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            string firstCommitSha;
            using (var repo = new Repository(dir))
            {
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A {}\n");
                Commands.Stage(repo, "A.cs");
                firstCommitSha = repo.Commit("v1", sig, sig).Sha;

                File.WriteAllText(Path.Combine(dir, "A.cs"), "class A { int x; }\n");
                Commands.Stage(repo, "A.cs");
                repo.Commit("v2", sig, sig);
            }

            var changed = new GitDiffProvider().GetChangedCSharpFiles(dir, firstCommitSha);
            Assert.Contains("A.cs", changed);

            Assert.Throws<DiffException>(() =>
                new GitDiffProvider().GetChangedCSharpFiles(dir, "no-such-ref"));
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
