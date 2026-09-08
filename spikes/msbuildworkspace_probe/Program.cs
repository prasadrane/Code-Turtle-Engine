using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

if (args.Length < 1) { Console.Error.WriteLine("usage: probe <path-to-csproj>"); return 2; }
var sw = Stopwatch.StartNew();
try
{
    using var workspace = MSBuildWorkspace.Create();
    workspace.RegisterWorkspaceFailedHandler(e => Console.Error.WriteLine($"[workspace-failed] {e.Diagnostic.Kind}: {e.Diagnostic.Message}"));
    var project = await workspace.OpenProjectAsync(args[0]);
    var compilation = await project.GetCompilationAsync();
    sw.Stop();
    if (compilation is null) { Console.Error.WriteLine("compilation null"); return 1; }
    var symbols = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).Count();
    var severity = compilation.GetTypeByMetadataName("CodeTurtleEngine.Core.Severity");
    Console.WriteLine($"OK ms={sw.ElapsedMilliseconds} trees={compilation.SyntaxTrees.Count()} typeSymbols={symbols}");
    Console.WriteLine($"Severity symbol resolved: {(severity is not null ? "yes" : "no")}");
    Console.WriteLine($"Diagnostics/errors={compilation.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error)}");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine($"FAIL {ex.GetType().Name}: {ex.Message}"); return 1; }
