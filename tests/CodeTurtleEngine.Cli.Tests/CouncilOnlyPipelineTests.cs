using System.Text.Json;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Cli.Tests;

public sealed class CouncilOnlyPipelineTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Runs_Only_Grounded_Personas_And_Writes_Unverified_Findings_With_Coverage()
    {
        var root = NewDirectory();
        try
        {
            var input = Path.Combine(root, "analysis.json");
            var output = Path.Combine(root, "findings.json");
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
            {
                ArchiveSha256 = Hash, Payload = Payload(), DiagnosticsCount = 0
            }, TurtleJson.Options));
            var runner = new CaptureRunner();
            var options = new CouncilOptions { MaxFiles = 1 };
            await new CouncilOnlyPipeline(runner, options).RunAsync(input, output);

            Assert.NotNull(runner.Payload);
            Assert.Single(runner.Payload.Files);
            Assert.Empty(runner.Sources!);
            Assert.False(string.IsNullOrWhiteSpace(runner.Rubric));
            var findings = JsonSerializer.Deserialize<ReviewFinding[]>(await File.ReadAllTextAsync(output), TurtleJson.Options);
            var finding = Assert.Single(findings!);
            Assert.Equal("Unverified proposal", finding.Title);
            var coverage = JsonSerializer.Deserialize<PromptCoverage>(await File.ReadAllTextAsync(
                Path.ChangeExtension(output, ".coverage.json")), TurtleJson.Options);
            Assert.Equal(new PromptCoverage(2, 1, 1, 2, 1, 1), coverage);
            using var participation = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.ChangeExtension(output, ".personas.json")));
            Assert.Equal(1, participation.RootElement.GetProperty("Successful").GetInt32());
            Assert.Equal(3, participation.RootElement.GetProperty("Expected").GetInt32());
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"ArchiveSha256\":\"bad\",\"Payload\":null}")]
    [InlineData("{\"ArchiveSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Payload\":{\"RepoSlug\":\"sealed-archive\",\"DiffBaseline\":\"wrong\",\"Files\":[]}}")]
    public async Task Rejects_Invalid_Analysis_Before_Personas_Or_Output(string json)
    {
        var root = NewDirectory();
        try
        {
            var input = Path.Combine(root, "analysis.json");
            var output = Path.Combine(root, "findings.json");
            await File.WriteAllTextAsync(input, json);
            var runner = new CaptureRunner();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new CouncilOnlyPipeline(runner, new()).RunAsync(input, output));
            Assert.Null(runner.Payload);
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Rejects_Naive_Mode_Without_Calling_Personas()
    {
        var runner = new CaptureRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CouncilOnlyPipeline(runner, new() { PromptMode = PromptMode.Naive }).RunAsync("unused", "unused"));
        Assert.Null(runner.Payload);
    }

    [Fact]
    public async Task Rejects_Empty_Payload_Before_Personas()
    {
        var root = NewDirectory();
        try
        {
            var input = Path.Combine(root, "analysis.json");
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
            {
                ArchiveSha256 = Hash, Payload = new RoslynPayload("sealed-archive", Hash, [])
            }, TurtleJson.Options));
            var runner = new CaptureRunner();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new CouncilOnlyPipeline(runner, new()).RunAsync(input, Path.Combine(root, "findings.json")));
            Assert.Null(runner.Payload);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-council-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static RoslynPayload Payload() => new("sealed-archive", Hash,
        new[] { "A.cs", "B.cs" }.Select(file => new FileFacts(file,
            [new MethodFacts("Demo.M", [], null, [], ["Demo.M"], 1, 1)])).ToArray());

    private sealed class CaptureRunner : IPersonaRunner
    {
        public RoslynPayload? Payload { get; private set; }
        public IReadOnlyList<SourceFile>? Sources { get; private set; }
        public string? Rubric { get; private set; }
        public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources,
            string rubric, CancellationToken ct = default)
        {
            Payload = payload; Sources = sources; Rubric = rubric;
            return Task.FromResult<IReadOnlyList<PersonaVerdict>>([new(PersonaRole.SecurityAuditor,
                [new(PersonaRole.SecurityAuditor, Severity.Warning, "Unverified proposal", "detail", "A.cs:1", ["Demo.M"])])]);
        }
    }
}
