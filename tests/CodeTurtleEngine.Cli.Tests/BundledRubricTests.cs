using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Cli.Tests;

[CollectionDefinition("Rubric working directory", DisableParallelization = true)]
public class RubricWorkingDirectoryCollection { }

[Collection("Rubric working directory")]
public class BundledRubricTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Review_From_Unrelated_Directory_Loads_Bundled_Default_Or_Explicit_Custom_Rubric(bool custom)
    {
        var previous = Directory.GetCurrentDirectory();
        var root = Path.Combine(Path.GetTempPath(), "turtle-bundled-rubric-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Directory.SetCurrentDirectory(root);
            var options = new TurtleOptions();
            string expected;
            if (custom)
            {
                options.RubricPath = "custom.md";
                expected = "Explicit custom rubric";
                await File.WriteAllTextAsync(Path.Combine(root, options.RubricPath), expected);
            }
            else
            {
                var bundled = Path.Combine(AppContext.BaseDirectory, "turtle", "rubric_v1.md");
                Assert.True(File.Exists(bundled), "The default rubric must travel with CLI binaries.");
                expected = await File.ReadAllTextAsync(bundled);
                Directory.CreateDirectory(Path.Combine(root, "turtle"));
                await File.WriteAllTextAsync(Path.Combine(root, "turtle", "rubric_v1.md"), "Wrong cwd rubric");
            }
            var personas = new RubricRecorder();
            var pipeline = new ReviewPipeline(new FakeDiff([]), new FakeLoader(), personas,
                new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(root), options);

            await pipeline.RunAsync(root, Path.Combine(root, "Sample.csproj"), null);

            Assert.Equal(expected, personas.Rubric);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(root, true);
        }
    }

    private sealed class RubricRecorder : IPersonaRunner
    {
        public string? Rubric { get; private set; }
        public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload,
            IReadOnlyList<SourceFile> sources, string rubric, CancellationToken ct = default)
        {
            Rubric = rubric;
            return Task.FromResult<IReadOnlyList<PersonaVerdict>>([]);
        }
    }
}
