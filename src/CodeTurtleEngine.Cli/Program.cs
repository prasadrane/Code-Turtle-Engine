using System.CommandLine;
using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Cli.Bench;
using Microsoft.Extensions.DependencyInjection;

var repoArg = new Argument<string>("repo") { Description = "Path to the target git repository" };
var projectOpt = new Option<string?>("--project") { Description = "Path to the .csproj/.sln to compile (default: discovered in repo)" };
var diffOpt = new Option<string?>("--diff") { Description = "Baseline ref (branch or SHA). Default: working tree vs HEAD" };
var outOpt = new Option<string?>("--out") { Description = "Also write the markdown review to this path" };

var review = new Command("review", "Review a repository's changed C# files");
review.Arguments.Add(repoArg);
review.Options.Add(projectOpt);
review.Options.Add(diffOpt);
review.Options.Add(outOpt);

review.SetAction(async (parseResult, ct) =>
{
    try
    {
        var repo = parseResult.GetValue(repoArg) ?? throw new InvalidOperationException("repo argument is required");
        var project = parseResult.GetValue(projectOpt);
        var diff = parseResult.GetValue(diffOpt);
        var outPath = parseResult.GetValue(outOpt);

        var services = Composition.BuildServices(repo);
        var pipeline = services.GetRequiredService<ReviewPipeline>();
        var projectPath = project ?? Composition.DiscoverProject(repo);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await pipeline.RunAsync(repo, projectPath, diff, ct).ConfigureAwait(false);
        sw.Stop();

        Console.Out.WriteLine(result.Markdown);
        if (outPath is not null) File.WriteAllText(outPath, result.Markdown);
        Console.Error.WriteLine($"Artifacts: {result.ArtifactDir}");
        Console.Error.WriteLine($"elapsed: {sw.ElapsedMilliseconds} ms");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});

var corpusOpt = new Option<string>("--corpus") { Description = "Path to the benchmark corpus manifest (JSON)", Required = true };
var repeatOpt = new Option<int>("--repeat") { Description = "Repetitions per arm per entry", DefaultValueFactory = _ => 3 };
var benchOutOpt = new Option<string?>("--out") { Description = "Output directory (default: ./bench-out/<UTC timestamp>)" };
var workOpt = new Option<string?>("--work") { Description = "Work directory for prepared repos (default: <temp>/turtle-bench-work)" };

var bench = new Command("bench", "Benchmark naive vs grounded review over a corpus");
bench.Options.Add(corpusOpt);
bench.Options.Add(repeatOpt);
bench.Options.Add(benchOutOpt);
bench.Options.Add(workOpt);

bench.SetAction(async (parseResult, ct) =>
{
    try
    {
        var corpus = CorpusLoader.Load(parseResult.GetValue(corpusOpt)!);
        var repeat = parseResult.GetValue(repeatOpt);
        if (repeat < 1) throw new BenchException("--repeat must be >= 1.");
        var outDir = Path.GetFullPath(parseResult.GetValue(benchOutOpt)
            ?? Path.Combine("bench-out", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)));
        var workDir = parseResult.GetValue(workOpt) ?? Path.Combine(Path.GetTempPath(), "turtle-bench-work");

        var llm = Composition.BuildServices(".").GetRequiredService<CodeTurtleEngine.Llm.LlmOptions>();
        BenchPreflight.RequireCredentials(llm, Environment.GetEnvironmentVariable);

        var pipelines = new[] { CodeTurtleEngine.Core.PromptMode.Naive, CodeTurtleEngine.Core.PromptMode.Grounded }
            .ToDictionary(m => m, m => Composition.BuildServices(".",
                    o => { o.Council.PromptMode = m; o.Council.Guard = CodeTurtleEngine.Core.GuardMode.Flag; },
                    home: Path.Combine(outDir, "runs", m.ToString().ToLowerInvariant()))
                .GetRequiredService<ReviewPipeline>());

        var preparer = new CorpusPreparer(workDir, DotnetRestore.RunAsync);
        var runner = new BenchRunner(m => pipelines[m], new CodeTurtleEngine.Gatekeeper.MsBuildCompilationLoader(), preparer.PrepareAsync, Console.Error);
        var routes = llm.Routes.Select(r => $"{r.Name}:{r.Model}").ToList();
        var results = await runner.RunAsync(corpus, repeat, routes, ct).ConfigureAwait(false);

        var (json, md) = BenchReport.Write(results, outDir);
        Console.Out.WriteLine(File.ReadAllText(md));
        Console.Error.WriteLine($"Results: {json}");
        return 0;
    }
    catch (BenchException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});

var root = new RootCommand("Code Turtle Engine - AI PR reviewer grounded in Roslyn compiler truths");
root.Subcommands.Add(review);
root.Subcommands.Add(bench);
return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
