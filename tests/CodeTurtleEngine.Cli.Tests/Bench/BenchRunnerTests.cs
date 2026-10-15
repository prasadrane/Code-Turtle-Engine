using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests.Bench;

public class BenchRunnerTests : IDisposable
{
    private const string Source = """
        namespace Demo
        {
            public class Dep { public void Go() { } }
            public class Outer
            {
                private readonly Dep _d;
                public Outer(Dep d) { _d = d; }
                public void Run() { _d.Go(); }
            }
        }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "turtle-bench-" + Guid.NewGuid().ToString("N"));
    private readonly string _rubric = Path.GetTempFileName();
    private readonly Compilation _compilation = CSharpCompilation.Create("Demo",
        new[] { CSharpSyntaxTree.ParseText(Source, path: "/repo/A.cs") },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

    public BenchRunnerTests() => File.WriteAllText(_rubric, "# Rubric");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, true);
        File.Delete(_rubric);
    }

    private sealed class ScriptedPersonas(Func<IReadOnlyList<PersonaVerdict>> script) : IPersonaRunner
    {
        public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources, string rubric, CancellationToken ct = default)
            => Task.FromResult(script());
    }

    private static IReadOnlyList<PersonaVerdict> Cites(params string[] fqns) => new[]
    {
        new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
        {
            new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "t", "d", "A.cs:8", fqns)
        })
    };

    private ReviewPipeline Pipeline(PromptMode mode, IPersonaRunner personas)
    {
        var options = new TurtleOptions { RubricPath = _rubric, Council = new CouncilOptions { Quorum = 1, PromptMode = mode, Guard = GuardMode.Flag } };
        return new ReviewPipeline(new FakeDiff(new[] { "A.cs" }), new FakeLoader(_compilation), personas,
            new Arbiter(), new TurtleShellGuard(options.Council), new RubricLoader(), new ArtifactWriter(_home), options);
    }

    private static readonly CorpusManifest Corpus = new(
        new[] { new CorpusEntry("e1", CorpusKind.Seeded, "Demo.csproj") },
        new Dictionary<string, PersonaRole> { ["SqlInjection"] = PersonaRole.SecurityAuditor });

    private static Task<PreparedEntry> Prepared(IReadOnlyList<ExpectedDefect>? expected = null) =>
        Task.FromResult(new PreparedEntry("/repo", "/repo/Demo.csproj", "base", "head", expected));

    [Fact]
    public async Task Scores_Naive_Grounded_And_CodeTurtle_Arms()
    {
        var runner = new BenchRunner(
            mode => Pipeline(mode, new ScriptedPersonas(() => mode == PromptMode.Naive
                ? Cites("Demo.Outer", "Demo.Ghost")
                : Cites("Demo.Dep", "Demo.Ghost"))),
            new FakeLoader(_compilation),
            (_, _) => Prepared(new[] { new ExpectedDefect("A.cs", 8, "SqlInjection") }),
            TextWriter.Null);

        var r = await runner.RunAsync(Corpus, repeat: 2, new[] { "route:model" }, default);

        Assert.Equal(6, r.Rows.Count); // 2 reps x 3 arms
        var naive = r.Rows.First(x => x.Arm == BenchArm.Naive).Score!;
        Assert.Equal(2, naive.Unverified); // neither Demo.Outer nor Demo.Ghost is in the allow-list
        Assert.Equal(1, naive.Invented);   // only Demo.Ghost does not exist
        var grounded = r.Rows.First(x => x.Arm == BenchArm.Grounded).Score!;
        Assert.Equal(1, grounded.Invented);
        var turtle = r.Rows.First(x => x.Arm == BenchArm.CodeTurtle).Score!;
        Assert.Equal(0, turtle.Invented);  // Strip pruned the invented citation from the mixed finding
        Assert.Equal(1, turtle.Matched);
        Assert.Equal("head", r.Metadata.EntryHeads["e1"]);
    }

    [Fact]
    public async Task Persona_Failure_Records_Error_Rows_And_Continues()
    {
        var runner = new BenchRunner(
            mode => Pipeline(mode, new ScriptedPersonas(() => mode == PromptMode.Naive
                ? throw new ProviderException("Council quorum not met", Array.Empty<string>())
                : Cites("Demo.Dep"))),
            new FakeLoader(_compilation), (_, _) => Prepared(), TextWriter.Null);

        var r = await runner.RunAsync(Corpus, 1, Array.Empty<string>(), default);

        var failed = Assert.Single(r.Rows, x => x.Arm == BenchArm.Naive);
        Assert.Null(failed.Score);
        Assert.Contains("quorum", failed.Error);
        Assert.NotNull(r.Rows.Single(x => x.Arm == BenchArm.Grounded).Score);
    }

    [Fact]
    public async Task Preparation_Failure_And_Unknown_Marker_Class_Skip_The_Entry()
    {
        var corpus = Corpus with { Entries = new[] { Corpus.Entries[0], Corpus.Entries[0] with { Id = "e2" } } };
        var runner = new BenchRunner(mode => Pipeline(mode, new ScriptedPersonas(() => Cites())),
            new FakeLoader(_compilation),
            (e, _) => e.Id == "e1"
                ? throw new BenchException("e1: boom")
                : Prepared(new[] { new ExpectedDefect("A.cs", 1, "NotAClass") }),
            TextWriter.Null);

        var r = await runner.RunAsync(corpus, 1, Array.Empty<string>(), default);

        Assert.Empty(r.Rows);
        Assert.Equal(new[] { "e1", "e2" }, r.Skipped.Select(s => s.EntryId));
        Assert.Contains("NotAClass", r.Skipped[1].Reason);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var runner = new BenchRunner(mode => Pipeline(mode, new ScriptedPersonas(() => { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); return Cites(); })),
            new FakeLoader(_compilation), (_, _) => Prepared(), TextWriter.Null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(Corpus, 1, Array.Empty<string>(), cts.Token));
    }
}
