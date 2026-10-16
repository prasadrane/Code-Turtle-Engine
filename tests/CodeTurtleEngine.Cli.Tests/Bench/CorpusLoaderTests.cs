using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class CorpusLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "turtle-corpus-" + Guid.NewGuid().ToString("N"));

    public CorpusLoaderTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "seeded", "base"));
        Directory.CreateDirectory(Path.Combine(_dir, "seeded", "e1"));
        File.WriteAllText(Path.Combine(_dir, "seeded", "base", "Shop.csproj"), "<Project />");
    }

    public void Dispose()
    {
        // git object files are read-only on Windows
        foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_dir, true);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "corpus.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Loads_Seeded_And_Oss_Entries_With_Resolved_Dirs()
    {
        var m = CorpusLoader.Load(Write($$"""
        { "classPersonas": { "SqlInjection": "SecurityAuditor" },
          "entries": [
            { "id": "e1", "kind": "Seeded", "project": "Shop.csproj", "baseDir": "seeded/base", "headDir": "seeded/e1" },
            { "id": "oss-1", "kind": "Oss", "project": "Lib.csproj", "cloneUrl": "https://example.invalid/r.git",
              "baseSha": "{{Sha}}", "headSha": "{{Sha}}", "expected": [ { "file": "X.cs", "line": 3, "class": "SqlInjection" } ] } ] }
        """));

        Assert.Equal(2, m.Entries.Count);
        Assert.Equal(Path.Combine(_dir, "seeded", "base"), m.Entries[0].BaseDir);
        Assert.Equal(CorpusKind.Oss, m.Entries[1].Kind);
        Assert.Equal(PersonaRole.SecurityAuditor, m.ClassPersonas["SqlInjection"]);
        Assert.Equal(3, m.Entries[1].Expected![0].Line);
    }

    [Theory]
    [InlineData("""{ "classPersonas": {}, "entries": [] }""", "at least one entry")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "Bad Id", "kind": "Seeded", "project": "Shop.csproj", "baseDir": "seeded/base", "headDir": "seeded/e1" } ] }""", "Bad Id")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "e1", "kind": "Seeded", "project": "Shop.csproj", "baseDir": "seeded/missing", "headDir": "seeded/e1" } ] }""", "baseDir")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "e1", "kind": "Seeded", "project": "Nope.csproj", "baseDir": "seeded/base", "headDir": "seeded/e1" } ] }""", "project")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "e1", "kind": "Seeded", "project": "Shop.csproj", "baseDir": "seeded/base", "headDir": "seeded/e1" }, { "id": "e1", "kind": "Seeded", "project": "Shop.csproj", "baseDir": "seeded/base", "headDir": "seeded/e1" } ] }""", "duplicate")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "o1", "kind": "Oss", "project": "L.csproj", "cloneUrl": "https://x.invalid/r.git", "baseSha": "abc", "headSha": "abc" } ] }""", "baseSha")]
    [InlineData("""{ "classPersonas": {}, "entries": [ { "id": "o1", "kind": "Oss", "project": "L.csproj", "cloneUrl": "https://x.invalid/r.git", "baseSha": "0123456789abcdef0123456789abcdef01234567", "headSha": "0123456789abcdef0123456789abcdef01234567", "expected": [ { "file": "X.cs", "line": 1, "class": "Unknown" } ] } ] }""", "Unknown")]
    public void Rejects_Invalid_Manifests(string json, string messageFragment)
    {
        var ex = Assert.Throws<BenchException>(() => CorpusLoader.Load(Write(json)));
        Assert.Contains(messageFragment, ex.Message);
    }

    [Fact]
    public async Task Shipped_Seeded_Corpus_Is_Valid_And_Every_Entry_Has_Expected_Markers()
    {
        var repoRoot = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repoRoot, "CodeTurtleEngine.sln"))) repoRoot = Path.GetDirectoryName(repoRoot)!;
        var corpus = CorpusLoader.Load(Path.Combine(repoRoot, "bench", "corpus.json"));
        var preparer = new CorpusPreparer(Path.Combine(_dir, "work"), (_, _) => Task.CompletedTask);

        foreach (var e in corpus.Entries)
        {
            var p = await preparer.PrepareAsync(e, default);
            if (e.Id == "clean-refactor") Assert.Empty(p.Expected!);
            else Assert.Single(p.Expected!);
            Assert.All(p.Expected!, d => Assert.True(corpus.ClassPersonas.ContainsKey(d.Class), d.Class));
        }
    }
}
