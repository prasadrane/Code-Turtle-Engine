using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests;

internal sealed class FakeDiff : IDiffProvider
{
    private readonly string[] _files;
    public FakeDiff(string[] files) => _files = files;
    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef) => _files;
}

internal sealed class FakeLoader : ICompilationLoader
{
    public Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default)
        => Task.FromResult(new LoadedCompilation(CSharpCompilation.Create("Stub"), new Noop()));

    private sealed class Noop : IDisposable { public void Dispose() { } }
}

internal sealed class FakePersonas : IPersonaRunner
{
    public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, string rubric, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PersonaVerdict>>(new[]
        {
            new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
            {
                new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection",
                    "raw concat", "DataAccess.cs:11", new[] { "SampleRepo.DataAccess" })
            })
        });
}
