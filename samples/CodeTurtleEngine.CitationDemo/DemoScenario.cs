using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.CitationDemo;

public static class DemoScenario
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };

    public static readonly IReadOnlyList<SourceFile> Sources = [
        new("Changed.cs", """
            namespace Demo;
            public class Changed
            {
                public void Review()
                {
                    new Dependency().Run(1);
                }
            }
            """.Replace("\r\n", "\n", StringComparison.Ordinal)),
        new("Dependency.cs", """
            namespace Demo;
            public class Dependency
            {
                public void Run(int value) { }
                public void Run(string value) { }
                public void Unrelated() { }
            }
            """.Replace("\r\n", "\n", StringComparison.Ordinal))
    ];

    public static DemoReport Run()
    {
        var compilation = CSharpCompilation.Create("CitationDemo",
            Sources.Select(s => CSharpSyntaxTree.ParseText(s.Text, path: s.Path)),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("Demonstration fixture did not compile.");
        ReviewFinding Finding(string title, string citation) => new(PersonaRole.SecurityAuditor, Severity.Info,
            title, "Authored suggestion to demonstrate the guard; this is not a verified defect.", "Changed.cs:6", [citation]);
        ReviewFinding[] original = [
            Finding("Valid declaration signature", "Demo.Dependency.Run(int)"),
            Finding("Fabricated overload", "Demo.Dependency.Run(bool)"),
            Finding("Fabricated member", "Demo.Dependency.DoesNotExist"),
            Finding("Real but unrelated member", "Demo.Dependency.Unrelated"),
            Finding("Name only: overload unspecified", "Demo.Dependency.Run")
        ];
        var payload = new PayloadGenerator(compilation).Build("citation-demo", "authored-fixture", ["Changed.cs"]);
        var guarded = new TurtleShellGuard(new CouncilOptions { Guard = GuardMode.Strip })
            .Verify(original, payload, citationResolver: new SymbolOracle(compilation).Resolve);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", Sources.Select(s => s.Path + "\n" + s.Text)))));
        return new("authored-fixture", "No model calls. Citation checks do not establish recommendation correctness.",
            hash, Sources, original, guarded);
    }
}

public sealed record DemoReport(string EvidenceCategory, string Limitations, string FixtureSha256,
    IReadOnlyList<SourceFile> Sources, IReadOnlyList<ReviewFinding> Original, GuardOutcome Outcome);
