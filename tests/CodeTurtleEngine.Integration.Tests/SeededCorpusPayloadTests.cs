using CodeTurtleEngine.Cli.Bench;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Integration.Tests;

public class SeededCorpusPayloadTests
{
    [Fact]
    public async Task All_12_Seeded_Benchmark_Entries_Surface_Deterministic_Signals()
    {
        var root = Path.Combine(Path.GetTempPath(), "turtle-seeded-payload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var manifestPath = Path.Combine(TestPaths.RepoRoot, "bench", "corpus.json");
            var manifest = CorpusLoader.Load(manifestPath);
            var preparer = new CorpusPreparer(Path.Combine(root, "work"), DotnetRestore.RunAsync);
            var loader = new MsBuildCompilationLoader();
            var diffProvider = new GitDiffProvider();

            foreach (var entry in manifest.Entries)
            {
                var prepared = await preparer.PrepareAsync(entry, CancellationToken.None);
                var loaded = await loader.LoadAsync(prepared.ProjectPath, CancellationToken.None);
                try
                {
                    var changed = diffProvider.GetChangedCSharpFiles(prepared.RepoDir, prepared.BaselineSha);
                    var payload = new PayloadGenerator(loaded.Compilation).Build("shop", prepared.BaselineSha, changed);

                    if (entry.Id == "clean-refactor")
                    {
                        Assert.NotEmpty(payload.Files);
                        foreach (var f in payload.Files)
                        {
                            foreach (var m in f.Methods)
                            {
                                Assert.Null(m.Risks);
                                Assert.Empty(m.Allocations);
                                Assert.Null(m.AsyncHealth);
                            }
                        }
                        continue;
                    }

                    Assert.NotNull(prepared.Expected);
                    Assert.Single(prepared.Expected);
                    var defect = prepared.Expected[0];

                    var fileFacts = payload.Files.FirstOrDefault(f => RepoPaths.EndsWith(f.FilePath, defect.File));
                    Assert.True(fileFacts is not null, $"File '{defect.File}' not found in payload for entry '{entry.Id}'.");

                    var method = fileFacts.Methods.FirstOrDefault(m =>
                        (m.StartLine <= defect.Line && m.EndLine >= defect.Line)
                        || (m.Risks?.Any(r => r.Contains($"Line {defect.Line}")) ?? false));
                    Assert.True(method is not null, $"No method covering defect line {defect.Line} in '{entry.Id}'.");

                    switch (entry.Id)
                    {
                        case "missing-configureawait":
                            Assert.NotNull(method.AsyncHealth);
                            Assert.Contains($"Line {defect.Line}", method.AsyncHealth);
                            break;
                        case "boxing":
                            Assert.Contains(method.Allocations, a => a.Contains("Boxing") && a.Contains($"Line {defect.Line}"));
                            break;
                        case "linq-closure":
                            Assert.Contains(method.Allocations, a => a.Contains("LINQ Closure") && a.Contains($"Line {defect.Line}"));
                            break;
                        case "fire-and-forget":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Unawaited Task") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "sync-over-async":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Sync-over-async") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "swallowed-exception":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Swallowed exception") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "hardcoded-secret":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Hardcoded secret") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "sql-injection":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Dynamic SQL") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "path-traversal":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Path.Combine") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "service-locator":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Service locator") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "captive-dependency":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("Captive dependency") && r.Contains($"Line {defect.Line}"));
                            break;
                        case "thread-unsafe-cache":
                            Assert.NotNull(method.Risks);
                            Assert.Contains(method.Risks, r => r.Contains("static non-thread-safe collection") && r.Contains($"Line {defect.Line}"));
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected entry id: {entry.Id}");
                    }
                }
                finally
                {
                    loaded.Workspace?.Dispose();
                }
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }
}
