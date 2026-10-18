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
            public class Other { }
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
        var selected = new List<int>();
        var runner = new BenchRunner(
            mode => Pipeline(mode, new ScriptedPersonas(() => mode == PromptMode.Naive
                ? Cites("Demo.Other", "Demo.Ghost")
                : Cites("Demo.Dep", "Demo.Ghost"))),
            new FakeLoader(_compilation),
            (_, _) => Prepared(new[] { new ExpectedDefect("A.cs", 8, "SqlInjection") }),
            TextWriter.Null, selected.Add);

        var r = await runner.RunAsync(Corpus, repeat: 2, new[] { "route:model" }, default);

        Assert.Equal(6, r.Rows.Count); // 2 reps x 3 arms
        Assert.Equal(new[] { 1, 2 }, selected);
        var naive = r.Rows.First(x => x.Arm == BenchArm.Naive).Score!;
        Assert.Equal(2, naive.Unverified); // neither Demo.Other nor Demo.Ghost is in the allow-list
        Assert.Equal(1, naive.Invented);   // only Demo.Ghost does not exist
        var grounded = r.Rows.First(x => x.Arm == BenchArm.Grounded).Score!;
        Assert.Equal(1, grounded.Invented);
        var turtle = r.Rows.First(x => x.Arm == BenchArm.CodeTurtle).Score!;
        Assert.Equal(0, turtle.Invented);  // Strip pruned the invented citation from the mixed finding
        Assert.Equal(1, turtle.Matched);
        Assert.Equal("head", r.Metadata.EntryHeads["e1"]);
    }

    [Fact]
    public async Task Derived_Arm_Retains_Compilation_Only_Citation_Without_Changing_Originals()
    {
        var calls = 0;
        var runner = new BenchRunner(mode => Pipeline(mode, new ScriptedPersonas(() =>
            { calls++; return Cites("Demo.Other", "Demo.Ghost"); })),
            new FakeLoader(_compilation), (_, _) => Prepared(), TextWriter.Null);
        var result = await runner.RunAsync(Corpus, 1, Array.Empty<string>(), default);
        var grounded = result.Rows.Single(r => r.Arm == BenchArm.Grounded).Score!;
        var guarded = result.Rows.Single(r => r.Arm == BenchArm.CodeTurtle).Score!;
        Assert.Equal(1, grounded.Invented);
        Assert.Equal(2, grounded.Unverified);
        Assert.Equal(0, guarded.Invented);
        Assert.Equal(1, guarded.Unverified); // real but outside payload remains
        Assert.Equal(2, calls); // Naive + Grounded, no additional persona call
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

    [Fact]
    public async Task IO_Failure_During_Prepare_Skips_The_Entry()
    {
        var runner = new BenchRunner(mode => Pipeline(mode, new ScriptedPersonas(() => Cites())),
            new FakeLoader(_compilation), (_, _) => throw new IOException("disk gone"), TextWriter.Null);

        var r = await runner.RunAsync(Corpus, 1, Array.Empty<string>(), default);

        Assert.Empty(r.Rows);
        Assert.Contains("disk gone", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public async Task Expected_Defects_Are_Enriched_With_Enclosing_Method()
    {
        var runner = new BenchRunner(mode => Pipeline(mode, new ScriptedPersonas(() => new[]
            {
                new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
                {
                    new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Warning, "bug in Run", "d", "A.cs", Array.Empty<string>())
                })
            })),
            new FakeLoader(_compilation),
            (_, _) => Prepared(new[] { new ExpectedDefect("A.cs", 8, "SqlInjection") }),
            TextWriter.Null);

        var r = await runner.RunAsync(Corpus, 1, Array.Empty<string>(), default);

        // location has no line; only the enriched method name (Run) can produce the match
        Assert.Equal(1, r.Rows.First(x => x.Arm == BenchArm.Grounded).Score!.Matched);
    }

    [Fact]
    public async Task EvaluateExitCode_Fails_On_Error_Degraded_Or_Skipped_In_Both_Live_And_Replay_Modes()
    {
        var runner = new BenchRunner(
            mode => Pipeline(mode, new ScriptedPersonas(() => new[]
            {
                new PersonaVerdict(PersonaRole.SecurityAuditor, Array.Empty<ReviewFinding>()),
                new PersonaVerdict(PersonaRole.AllocationsPerformance, Array.Empty<ReviewFinding>()),
                new PersonaVerdict(PersonaRole.IdiomaticArchitect, Array.Empty<ReviewFinding>())
            })),
            new FakeLoader(_compilation), (_, _) => Prepared(), TextWriter.Null);

        var ok = await runner.RunAsync(Corpus, 1, new[] { "route:model" }, default);
        Assert.True(ok.IsComplete);
        Assert.Equal(0, ok.EvaluateExitCode(isReplay: false, TextWriter.Null));
        Assert.Equal(0, ok.EvaluateExitCode(isReplay: true, TextWriter.Null));

        // Degraded row (2 of 3 personas) -> exit 1 in both live and replay
        var degraded = ok with { Rows = ok.Rows.Select(r => r with { PersonasSucceeded = 2 }).ToArray() };
        Assert.False(degraded.IsComplete);
        var liveErr = new StringWriter();
        Assert.Equal(1, degraded.EvaluateExitCode(isReplay: false, liveErr));
        Assert.Contains("Benchmark run is incomplete", liveErr.ToString());

        // Error row -> exit 1 in both live and replay
        var errored = ok with { Rows = ok.Rows.Select(r => r with { Error = "403", Score = null }).ToArray() };
        var replayErr = new StringWriter();
        Assert.Equal(1, errored.EvaluateExitCode(isReplay: true, replayErr));
        Assert.Contains("Replay is incomplete", replayErr.ToString());

        // Skipped entry -> exit 1 in both live and replay
        var skipped = ok with { Skipped = new[] { new SkippedEntry("e2", "compile failed") } };
        Assert.Equal(1, skipped.EvaluateExitCode(isReplay: false, TextWriter.Null));
        Assert.Equal(1, skipped.EvaluateExitCode(isReplay: true, TextWriter.Null));
    }
}
