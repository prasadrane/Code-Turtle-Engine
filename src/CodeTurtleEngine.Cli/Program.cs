using System.CommandLine;
using CodeTurtleEngine.Cli;
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

var root = new RootCommand("Code Turtle Engine - AI PR reviewer grounded in Roslyn compiler truths");
root.Subcommands.Add(review);
return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
