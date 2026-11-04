using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli.Tests;

public class CompilationCoveragePipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Review_And_Extraction_Stop_When_Changed_Source_Is_Outside_Selected_Project(bool imported)
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-coverage-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var omitted = Path.Combine(root, "Other.cs");
            await File.WriteAllTextAsync(omitted, "class Other { void M() {} }");
            var rubric = Path.Combine(root, "rubric.md");
            await File.WriteAllTextAsync(rubric, "Review rubric");
            var diff = new FakeDiff(["Other.cs"]);
            var loader = new FakeLoader();
            var personas = new RecordingPersonas();
            var options = new TurtleOptions { RubricPath = rubric };
            var review = new ReviewPipeline(diff, loader, personas, new Arbiter(),
                new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(root), options);

            var error = await Assert.ThrowsAsync<CompilationException>(() =>
                review.RunAsync(root, Path.Combine(root, "Selected.csproj"), null,
                    imported ? new RoslynPayload("imported", "HEAD", []) : null));
            Assert.Contains("Other.cs", error.Message);
            Assert.Null(personas.LastSources);
            await Assert.ThrowsAsync<CompilationException>(() =>
                new ExtractionPipeline(diff, loader).ExtractAsync(root, Path.Combine(root, "Selected.csproj"), null));
        }
        finally { Directory.Delete(root, true); }
    }
}
