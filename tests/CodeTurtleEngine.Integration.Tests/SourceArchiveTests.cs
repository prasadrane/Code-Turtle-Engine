using System.Formats.Tar;
using System.Text;
using CodeTurtleEngine.Worker;

namespace CodeTurtleEngine.Integration.Tests;

public sealed class SourceArchiveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "turtle-archive-" + Guid.NewGuid());

    public SourceArchiveTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Extract_PreservesNestedSourcesUnderCanonicalDestination()
    {
        var archive = Archive(("src/C.cs", "class C {}"));
        var destination = Path.Combine(_root, "repo") + Path.DirectorySeparatorChar;

        SourceArchive.Extract(archive, destination);

        Assert.Equal("class C {}", File.ReadAllText(Path.Combine(destination, "src", "C.cs")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/../../escape")]
    [InlineData("..\\escape")]
    [InlineData("/escape")]
    [InlineData("C:/escape")]
    public void Extract_RejectsEscapingPaths(string path)
    {
        var archive = Archive((path, "untrusted"));

        Assert.Throws<InvalidDataException>(() => SourceArchive.Extract(archive, Path.Combine(_root, "repo")));
        Assert.False(File.Exists(Path.Combine(_root, "escape")));
    }

    [Fact]
    public void Extract_RejectsSymbolicLinks()
    {
        var archive = Path.Combine(_root, "source.tar");
        using (var writer = new TarWriter(File.Create(archive)))
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "src") { LinkName = "/outside" });

        Assert.Throws<InvalidDataException>(() => SourceArchive.Extract(archive, Path.Combine(_root, "repo")));
    }

    [Fact]
    public void Extract_RejectsDuplicateEntriesInsteadOfReplacingSource()
    {
        var archive = Archive(("C.cs", "original"), ("C.cs", "replacement"));
        var destination = Path.Combine(_root, "repo");

        Assert.Throws<IOException>(() => SourceArchive.Extract(archive, destination));
        Assert.Equal("original", File.ReadAllText(Path.Combine(destination, "C.cs")));
    }

    private string Archive(params (string Path, string Content)[] entries)
    {
        var archive = Path.Combine(_root, "source.tar");
        using var writer = new TarWriter(File.Create(archive));
        foreach (var (path, content) in entries)
        {
            using var data = new MemoryStream(Encoding.UTF8.GetBytes(content));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = data });
        }
        return archive;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
