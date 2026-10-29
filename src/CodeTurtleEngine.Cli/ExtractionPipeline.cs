using System.Text.Json;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;

namespace CodeTurtleEngine.Cli;

public sealed class ExtractionPipeline
{
    private readonly IDiffProvider _diff;
    private readonly ICompilationLoader _loader;

    public ExtractionPipeline(IDiffProvider diff, ICompilationLoader loader)
    {
        _diff = diff;
        _loader = loader;
    }

    public async Task<RoslynPayload> ExtractAsync(string repoPath, string projectPath, string? baselineRef, CancellationToken ct = default)
    {
        var changed = _diff.GetChangedCSharpFiles(repoPath, baselineRef);
        var hunks = _diff.GetDiffHunks(repoPath, baselineRef);
        var loaded = await _loader.LoadAsync(projectPath, changed, ct).ConfigureAwait(false);
        try
        {
            var repoSlug = Path.GetFileNameWithoutExtension(projectPath);
            return new PayloadGenerator(loaded.Projects).Build(repoSlug, baselineRef ?? "working-tree", changed, hunks);
        }
        finally
        {
            loaded.Workspace.Dispose();
        }
    }

    public string SerializePayload(RoslynPayload payload)
    {
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public RoslynPayload DeserializePayload(string json)
    {
        return JsonSerializer.Deserialize<RoslynPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize RoslynPayload.");
    }
}
