using CodeTurtleEngine.Core;
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

    [Fact]
    public void Deletion_Only_Edit_Produces_Zero_Head_Hunks_And_Drops_Untouched_Sarif()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-del-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                var lines = Enumerable.Range(1, 20).Select(i => $"// line {i}").ToList();
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);
                Commands.Stage(repo, "F.cs");
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                repo.Commit("v1", sig, sig);

                lines.RemoveAt(9);
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);

                var hunks = new GitDiffProvider().GetDiffHunks(dir, null);
                Assert.Empty(hunks);

                var diags = new[] { new SarifDiagnostic("CA3001", "old issue", "F.cs", 7, 7) };
                var intersected = SarifTriagePipeline.IntersectWithDiffHunks(diags, hunks);
                Assert.Empty(intersected);
            }
        }
        finally
        {
            ClearAttributes(dir);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Insertion_Edit_Produces_Exact_Head_Hunk_For_Inserted_Lines()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-ins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                var lines = Enumerable.Range(1, 20).Select(i => $"// line {i}").ToList();
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);
                Commands.Stage(repo, "F.cs");
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                repo.Commit("v1", sig, sig);

                lines.Insert(10, "// new 1");
                lines.Insert(11, "// new 2");
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);

                var hunks = new GitDiffProvider().GetDiffHunks(dir, null);
                var hunk = Assert.Single(hunks);
                Assert.Equal(11, hunk.StartLine);
                Assert.Equal(2, hunk.LineCount);

                var diags = new[]
                {
                    new SarifDiagnostic("CA1", "in hunk", "F.cs", 11, 11),
                    new SarifDiagnostic("CA2", "before hunk", "F.cs", 10, 10),
                    new SarifDiagnostic("CA3", "after hunk", "F.cs", 13, 13)
                };
                var intersected = SarifTriagePipeline.IntersectWithDiffHunks(diags, hunks);
                Assert.Single(intersected);
                Assert.Equal("CA1", intersected[0].RuleId);
            }
        }
        finally
        {
            ClearAttributes(dir);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Replacement_And_Separated_Edits_Produce_Distinct_Hunks_Excluding_Context()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-sep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                var lines = Enumerable.Range(1, 20).Select(i => $"// line {i}").ToList();
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);
                Commands.Stage(repo, "F.cs");
                var sig = new Signature("t", "t@t", DateTimeOffset.Now);
                repo.Commit("v1", sig, sig);

                lines[4] = "// replaced 5";
                lines[14] = "// replaced 15";
                File.WriteAllLines(Path.Combine(dir, "F.cs"), lines);

                var hunks = new GitDiffProvider().GetDiffHunks(dir, null);
                Assert.Equal(2, hunks.Count);
                Assert.Equal(5, hunks[0].StartLine);
                Assert.Equal(1, hunks[0].LineCount);
                Assert.Equal(15, hunks[1].StartLine);
                Assert.Equal(1, hunks[1].LineCount);

                var diags = new[]
                {
                    new SarifDiagnostic("CA1", "line 5", "F.cs", 5, 5),
                    new SarifDiagnostic("CA2", "context line 10", "F.cs", 10, 10),
                    new SarifDiagnostic("CA3", "line 15", "F.cs", 15, 15)
                };
                var intersected = SarifTriagePipeline.IntersectWithDiffHunks(diags, hunks);
                Assert.Equal(2, intersected.Count);
                Assert.Contains(intersected, d => d.RuleId == "CA1");
                Assert.Contains(intersected, d => d.RuleId == "CA3");
                Assert.DoesNotContain(intersected, d => d.RuleId == "CA2");
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
