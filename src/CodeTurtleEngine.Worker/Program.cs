using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Worker;

var output = Console.Out;
var stage = "request";
try
{
    var raw = await Console.In.ReadToEndAsync();
    if (raw.Length > 1024 * 1024) throw new InvalidDataException();
    var request = JsonSerializer.Deserialize<WorkerRequest>(raw, TurtleJson.Options) ?? throw new InvalidDataException();
    var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync("/input/source.tar")));
    Console.SetOut(TextWriter.Null);
    if (request.Operation == "MemoryProbe") WorkerProbe.ExhaustMemory();
    object result;
    if (request.Operation == "Probe") result = await WorkerProbe.RunAsync(hash);
    else
    {
        const string root = "/work/repo";
        stage = "archive";
        SourceArchive.Extract("/input/source.tar", root);
        string Contained(string rel)
        {
            var path = Path.GetFullPath(Path.Combine(root, rel));
            if (!path.StartsWith(root + "/", StringComparison.Ordinal)) throw new InvalidDataException();
            return path;
        }
        var project = Contained(request.Project);
        if (!File.Exists(project)) throw new InvalidDataException();
        foreach (var file in request.ChangedFiles)
            if (!File.Exists(Contained(file))) throw new InvalidDataException("Changed file omitted.");
        // The only feed is immutable, operator-supplied image content. No credentials or network.
        stage = "restore";
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "restore", project, "--source", "/opt/offline-feed", "--disable-parallel", "-p:NuGetAudit=false" })
            start.ArgumentList.Add(arg);
        using (var process = Process.Start(start) ?? throw new InvalidOperationException())
        {
            var stdout = Drain(process.StandardOutput);
            var stderr = Drain(process.StandardError);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0) throw new CompilationException("Offline restore failed.");
        }
        stage = "compilation";
        var loaded = await new MsBuildCompilationLoader().LoadAsync(project);
        using (loaded.Workspace)
        {
            if (request.ChangedFiles.Any(file => !loaded.Projects.SelectMany(c => c.SyntaxTrees)
                .Any(tree => RepoPaths.EndsWith(tree.FilePath, file))))
                throw new CompilationException("Changed file outside compilation.");
            var payload = new PayloadGenerator(loaded.Projects).Build("sealed-archive", hash, request.ChangedFiles);
            var oracle = new SymbolOracle(loaded.Projects);
            if (request.Operation == "Analyze")
                result = new { ArchiveSha256 = hash, Payload = payload, DiagnosticsCount = loaded.Diagnostics.Count };
            else if (request.Operation == "Audit")
            {
                if (request.Findings.Any(f => f.CitedSymbolFqns is null || f.Title is null || f.Detail is null || f.Location is null))
                    throw new InvalidDataException();
                var merged = new Arbiter().Merge(request.Findings.GroupBy(f => f.Persona).Select(g => new PersonaVerdict(g.Key, g.ToList())).ToList());
                var guarded = new TurtleShellGuard(new CouncilOptions { Guard = request.Guard, RequireCitationsInStripMode = request.Strict })
                    .Verify(merged, payload, citationResolver: oracle.Resolve);
                result = new { ArchiveSha256 = hash, Outcome = guarded, Markdown = new Arbiter().RenderMarkdown(guarded.KeptFindings, payload) };
            }
            else throw new InvalidDataException("Unknown operation.");
        }
    }
    await output.WriteAsync(JsonSerializer.Serialize(result, TurtleJson.Options));
    return 0;
}
catch (Exception ex)
{
    // Untrusted diagnostics may contain source text or attempted secret values.
    await Console.Error.WriteLineAsync($"Worker failed at {stage} ({ex.GetType().Name}).");
    return 1;
}

static async Task Drain(StreamReader reader)
{
    var buffer = new char[4096];
    while (await reader.ReadAsync(buffer) > 0) { }
}

internal sealed record WorkerRequest(string Operation, string Project, IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<ReviewFinding> Findings, GuardMode Guard = GuardMode.Strip, bool Strict = false);
