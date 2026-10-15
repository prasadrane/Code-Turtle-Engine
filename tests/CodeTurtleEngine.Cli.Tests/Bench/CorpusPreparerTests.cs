using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Gatekeeper;
using LibGit2Sharp;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class CorpusPreparerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "turtle-prep-" + Guid.NewGuid().ToString("N"));
    private string BaseDir => Path.Combine(_root, "base");
    private string HeadDir => Path.Combine(_root, "head");
    private string Work => Path.Combine(_root, "work");
    private readonly List<string> _restored = new();

    public CorpusPreparerTests()
    {
        Directory.CreateDirectory(Path.Combine(BaseDir, "Services"));
        Directory.CreateDirectory(Path.Combine(BaseDir, "obj"));
        Directory.CreateDirectory(Path.Combine(HeadDir, "Services"));
        File.WriteAllText(Path.Combine(BaseDir, "Shop.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(BaseDir, "obj", "junk.txt"), "ignored");
        File.WriteAllText(Path.Combine(BaseDir, "Services", "Repo.cs"), "class Repo\n{\n    void M() { }\n}\n");
        File.WriteAllText(Path.Combine(HeadDir, "Services", "Repo.cs"),
            "class Repo\n{\n    void M() { var q = \"x\" + y; } // BENCH: SqlInjection\n}\n");
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private CorpusPreparer Preparer() => new(Work, (p, _) => { _restored.Add(p); return Task.CompletedTask; });

    private CorpusEntry Seeded(string head) => new("e1", CorpusKind.Seeded, "Shop.csproj", BaseDir, head);

    [Fact]
    public async Task Seeded_Entry_Becomes_Repo_With_Base_And_Head_Commits()
    {
        var p = await Preparer().PrepareAsync(Seeded(HeadDir), default);

        Assert.Equal(new[] { "Services/Repo.cs" }, new GitDiffProvider().GetChangedCSharpFiles(p.RepoDir, p.BaselineSha));
        using var repo = new Repository(p.RepoDir);
        Assert.Equal(p.HeadSha, repo.Head.Tip.Sha);
        Assert.False(File.Exists(Path.Combine(p.RepoDir, "obj", "junk.txt")));
        Assert.Equal(new[] { Path.Combine(p.RepoDir, "Shop.csproj") }, _restored);
    }

    [Fact]
    public async Task Markers_Become_Expected_Defects_And_Are_Stripped()
    {
        var p = await Preparer().PrepareAsync(Seeded(HeadDir), default);

        var d = Assert.Single(p.Expected!);
        Assert.Equal(new ExpectedDefect("Services/Repo.cs", 3, "SqlInjection"), d);
        var text = File.ReadAllText(Path.Combine(p.RepoDir, "Services", "Repo.cs"));
        Assert.DoesNotContain("BENCH", text);
        Assert.Contains("var q = \"x\" + y; }\n", text);
    }

    [Fact]
    public async Task Rerun_Into_Same_Work_Dir_Rebuilds_Fresh()
    {
        await Preparer().PrepareAsync(Seeded(HeadDir), default);
        var p = await Preparer().PrepareAsync(Seeded(HeadDir), default);

        using var repo = new Repository(p.RepoDir);
        Assert.Equal(2, repo.Commits.Count());
    }

    [Fact]
    public async Task Head_Identical_To_Base_Is_Rejected()
    {
        var ex = await Assert.ThrowsAsync<BenchException>(() => Preparer().PrepareAsync(Seeded(BaseDir), default));
        Assert.Contains("does not change the base", ex.Message);
    }
}
