using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Cli.Tests;

public class ArtifactWriterTests
{
    [Fact]
    public void Writes_All_Artifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-art-" + Guid.NewGuid().ToString("N"));
        var writer = new ArtifactWriter(root);
        var payload = new RoslynPayload("Sample.Repo", "HEAD", Array.Empty<FileFacts>());
        var verdict = new CouncilVerdict(Array.Empty<PersonaVerdict>(), Array.Empty<ReviewFinding>(),
            new[] { new GuardResult("X", true) });

        var dir = writer.Write("Sample.Repo", payload, verdict, "# review");

        Assert.True(File.Exists(Path.Combine(dir, "review.md")));
        Assert.True(File.Exists(Path.Combine(dir, "payload.json")));
        Assert.True(File.Exists(Path.Combine(dir, "verdicts.json")));
        Assert.True(File.Exists(Path.Combine(dir, "audit.json")));
        Directory.Delete(root, true);
    }
}
