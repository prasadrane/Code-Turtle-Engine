using System.Text.Json;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests;

public class PromptCoveragePipelineTests
{
    [Fact]
    public async Task Report_And_Artifacts_Disclose_File_And_Member_Omissions_Without_Trimming_Guard_Payload()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-prompt-coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var names = new[] { "A.cs", "B.cs", "C.cs" };
            var trees = names.Select(name => CSharpSyntaxTree.ParseText(
                $"class {Path.GetFileNameWithoutExtension(name)} {{ void One() {{}} void Two() {{}} void Three() {{}} }}",
                path: Path.Combine(root, name))).ToArray();
            foreach (var tree in trees) await File.WriteAllTextAsync(tree.FilePath, tree.GetText().ToString());
            var rubric = Path.Combine(root, "rubric.md");
            await File.WriteAllTextAsync(rubric, "Review rubric");
            var options = new TurtleOptions { RubricPath = rubric,
                Council = new CouncilOptions { MaxFiles = 1, MaxMethodsPerFile = 2 } };
            var personas = new CapturePersonas();
            var result = await new ReviewPipeline(new FakeDiff(names), new FakeLoader(CSharpCompilation.Create("Demo", trees)),
                personas, new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(root), options)
                .RunAsync(root, Path.Combine(root, "Demo.csproj"), null);

            Assert.Single(personas.Payload!.Files);
            Assert.Equal(2, personas.Payload.Files[0].Methods.Count);
            Assert.Equal(3, result.Payload.Files.Count);
            Assert.Equal(9, result.Payload.Files.Sum(f => f.Methods.Count));
            Assert.Equal(new PromptCoverage(3, 1, 2, 9, 2, 7), result.Verdict.PromptCoverage);
            Assert.Contains("3 extracted, 1 submitted to model prompts, 2 omitted", result.Markdown);
            Assert.Contains("9 extracted, 2 submitted to model prompts, 7 omitted", result.Markdown);
            Assert.Contains("Partial model input", result.Markdown);
            Assert.DoesNotContain("Files reviewed", result.Markdown);
            var saved = JsonSerializer.Deserialize<PromptCoverage>(await File.ReadAllTextAsync(
                Path.Combine(result.ArtifactDir, "prompt-coverage.json")), TurtleJson.Options);
            Assert.Equal(result.Verdict.PromptCoverage, saved);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class CapturePersonas : IPersonaRunner
    {
        public RoslynPayload? Payload { get; private set; }
        public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources,
            string rubric, CancellationToken ct = default)
        {
            Payload = payload;
            return Task.FromResult<IReadOnlyList<PersonaVerdict>>([new(PersonaRole.SecurityAuditor, [])]);
        }
    }
}
