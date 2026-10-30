using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests;

internal sealed class FakeDiff : IDiffProvider
{
    private readonly string[] _files;
    private readonly IReadOnlyList<DiffHunk> _hunks;
    public FakeDiff(string[] files, IReadOnlyList<DiffHunk>? hunks = null)
    {
        _files = files;
        _hunks = hunks ?? files.Select(f => new DiffHunk(f, 1, 100)).ToList();
    }
    public IReadOnlyList<string> GetChangedCSharpFiles(string repoPath, string? baselineRef) => _files;
    public IReadOnlyList<DiffHunk> GetDiffHunks(string repoPath, string? baselineRef) => _hunks;
}

internal sealed class FakeLoader : ICompilationLoader
{
    private readonly Compilation _compilation;
    private readonly string[] _degraded;
    public FakeLoader(Compilation? compilation = null, string[]? degraded = null)
    {
        _compilation = compilation ?? CSharpCompilation.Create("Stub");
        _degraded = degraded ?? Array.Empty<string>();
    }

    public Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, CancellationToken ct = default)
        => LoadAsync(projectOrSolutionPath, null, ct);

    public Task<LoadedCompilation> LoadAsync(string projectOrSolutionPath, IReadOnlyList<string>? changedFiles, CancellationToken ct = default)
        => Task.FromResult(new LoadedCompilation(_compilation, new Noop()) { DegradedProjects = _degraded });

    private sealed class Noop : IDisposable { public void Dispose() { } }
}

internal sealed class FakePersonas : IPersonaRunner
{
    public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources, string rubric, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PersonaVerdict>>(new[]
        {
            new PersonaVerdict(PersonaRole.SecurityAuditor, new[]
            {
                new ReviewFinding(PersonaRole.SecurityAuditor, Severity.Error, "SQL injection",
                    "raw concat", "DataAccess.cs:11", new[] { "SampleRepo.DataAccess" })
            })
        });
}

internal sealed class RecordingPersonas : IPersonaRunner
{
    public IReadOnlyList<SourceFile>? LastSources { get; private set; }

    public Task<IReadOnlyList<PersonaVerdict>> RunAsync(RoslynPayload payload, IReadOnlyList<SourceFile> sources, string rubric, CancellationToken ct = default)
    {
        LastSources = sources;
        return Task.FromResult<IReadOnlyList<PersonaVerdict>>(new[] { new PersonaVerdict(PersonaRole.SecurityAuditor, Array.Empty<ReviewFinding>()) });
    }
}
