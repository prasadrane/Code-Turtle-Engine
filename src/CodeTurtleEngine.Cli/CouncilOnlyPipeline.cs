using System.Text;
using System.Text.Json;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Cli;

/// <summary>
/// Credentialed model transport for a trusted orchestrator's isolated Analyze response.
/// Produces proposals only: the orchestrator must Audit them against the same sealed archive.
/// This path never loads a project, a source checkout, or repository configuration.
/// </summary>
public sealed class CouncilOnlyPipeline(IPersonaRunner personas, CouncilOptions options)
{
    private const int MaxAnalysisBytes = 8 * 1024 * 1024;
    private const int MaxFindingsBytes = 1024 * 1024;

    public async Task RunAsync(string analysisPath, string outputPath, CancellationToken ct = default)
    {
        if (options.PromptMode != PromptMode.Grounded)
            throw new InvalidOperationException("Council-only transport requires grounded prompts.");
        if (File.Exists(outputPath)) throw new IOException("Use a fresh findings output path.");
        await using var stream = File.OpenRead(analysisPath);
        if (stream.Length > MaxAnalysisBytes) throw new InvalidDataException("Analyze response exceeds limit.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);
        WorkerAnalysis? analysis;
        try { analysis = JsonSerializer.Deserialize<WorkerAnalysis>(bytes, TurtleJson.Options); }
        catch (JsonException) { throw new InvalidDataException("Invalid Analyze response."); }
        Validate(analysis);
        var payload = analysis!.Payload!;
        var rubric = new RubricLoader().Load(Path.Combine(AppContext.BaseDirectory, "turtle", "rubric_v1.md"));
        var prompt = PayloadTrimmer.TrimForPrompt(payload, options);
        var coverage = PayloadTrimmer.MeasureCoverage(payload, prompt);
        var verdicts = await personas.RunAsync(prompt, Array.Empty<SourceFile>(), rubric, ct).ConfigureAwait(false);
        // Deliberately do not merge, verify or render these untrusted model proposals here.
        var findings = verdicts.SelectMany(verdict => verdict.Findings).ToArray();
        var json = JsonSerializer.Serialize(findings, TurtleJson.Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxFindingsBytes)
            throw new InvalidDataException("Model proposals exceed worker request limit.");
        await File.WriteAllTextAsync(Path.ChangeExtension(outputPath, ".coverage.json"),
            JsonSerializer.Serialize(coverage, TurtleJson.Options), ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.ChangeExtension(outputPath, ".personas.json"),
            JsonSerializer.Serialize(new { Successful = verdicts.Count, Expected = 3 }, TurtleJson.Options), ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, json, ct).ConfigureAwait(false);
    }

    private static void Validate(WorkerAnalysis? analysis)
    {
        var hash = analysis?.ArchiveSha256;
        var payload = analysis?.Payload;
        if (hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit) || payload is null ||
            payload.RepoSlug != "sealed-archive" || !string.Equals(hash, payload.DiffBaseline, StringComparison.OrdinalIgnoreCase) ||
            payload.Files is not { Count: > 0 } || payload.Files.Any(file => file is null ||
                string.IsNullOrWhiteSpace(file.FilePath) || file.Methods is null || file.Methods.Any(method =>
                    method is null || string.IsNullOrWhiteSpace(method.Method) || method.Allocations is null ||
                    method.Dependencies is null || method.ResolvedSymbols is null)))
            throw new InvalidDataException("Analyze response lacks pinned compilation facts.");
    }

    private sealed record WorkerAnalysis(string? ArchiveSha256, RoslynPayload? Payload);
}
