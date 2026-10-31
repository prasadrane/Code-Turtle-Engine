using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;
using LibGit2Sharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class DiffProviderTests
{
    private sealed class FilesOnlyDiffProvider : IDiffProvider
    {
        public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef) => ["A.cs"];
    }

    [Fact]
    public void Provider_Without_Hunk_Support_Reports_No_Evidence_Not_Empty_Hunks()
    {
        IDiffProvider provider = new FilesOnlyDiffProvider();
        Assert.Null(provider.GetDiffHunks("repo", "main"));
    }

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
                Assert.NotNull(hunks);
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
                Assert.NotNull(hunks);
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
                Assert.NotNull(hunks);
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

    [Fact]
    public void Advanced_Base_After_Fork_Does_Not_Report_Base_Only_Line_Changes_When_UseMergeBase_Is_True()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-mergebase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using var repo = new Repository(dir);
            var sig = new Signature("t", "t@t", DateTimeOffset.Now);

            // 1. Initial commit (base fork point)
            var initialLines = new[] { "// line 1", "// line 2", "// line 3" };
            File.WriteAllLines(Path.Combine(dir, "Service.cs"), initialLines);
            Commands.Stage(repo, "Service.cs");
            var initialCommit = repo.Commit("initial", sig, sig);

            // 2. Feature branch from initial
            repo.CreateBranch("feature", initialCommit);
            Commands.Checkout(repo, repo.Branches["feature"]);

            // Modify line 2 on feature
            var featureLines = new[] { "// line 1", "// line 2 modified on feature", "// line 3" };
            File.WriteAllLines(Path.Combine(dir, "Service.cs"), featureLines);
            Commands.Stage(repo, "Service.cs");
            repo.Commit("feature commit", sig, sig);

            // 3. Switch back to master and advance master with a change on line 1
            Commands.Checkout(repo, repo.Branches["master"] ?? repo.Branches["main"]);
            var masterLines = new[] { "// line 1 modified on master", "// line 2", "// line 3" };
            File.WriteAllLines(Path.Combine(dir, "Service.cs"), masterLines);
            Commands.Stage(repo, "Service.cs");
            var masterCommit = repo.Commit("master advanced", sig, sig);

            // Switch HEAD back to feature branch for review
            Commands.Checkout(repo, repo.Branches["feature"]);

            var diffProvider = new GitDiffProvider();

            // When diffing directly against master without merge-base:
            // Master has "// line 1 modified on master" and feature has "// line 1", so line 1 is in the diff!
            var hunksWithoutMergeBase = diffProvider.GetDiffHunks(dir, masterCommit.Sha, useMergeBase: false);
            Assert.NotNull(hunksWithoutMergeBase);
            Assert.Contains(hunksWithoutMergeBase, h => h.StartLine == 1); // reports base-only change

            // When diffing with useMergeBase: true:
            // Diff is computed from merge-base (initial) to feature, so ONLY line 2 is reported!
            var hunksWithMergeBase = diffProvider.GetDiffHunks(dir, masterCommit.Sha, useMergeBase: true);
            Assert.NotNull(hunksWithMergeBase);
            Assert.DoesNotContain(hunksWithMergeBase, h => h.StartLine == 1);
            var featureHunk = Assert.Single(hunksWithMergeBase);
            Assert.Equal(2, featureHunk.StartLine);
            Assert.Equal(1, featureHunk.LineCount);
        }
        finally
        {
            ClearAttributes(dir);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetDiffHunks_Returns_Null_When_MergeBase_Cannot_Be_Found()
    {
        var dir = Path.Combine(Path.GetTempPath(), "turtle-diff-disconnected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Repository.Init(dir);
            using var repo = new Repository(dir);
            var sig = new Signature("t", "t@t", DateTimeOffset.Now);

            File.WriteAllText(Path.Combine(dir, "A.cs"), "class A {}\n");
            Commands.Stage(repo, "A.cs");
            repo.Commit("c1", sig, sig);

            // Disconnected branch or invalid commit lookup
            var hunks = new GitDiffProvider().GetDiffHunks(dir, "0000000000000000000000000000000000000000", useMergeBase: true);
            Assert.Null(hunks);
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
