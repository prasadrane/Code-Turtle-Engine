using CodeTurtleEngine.Core;

namespace CodeTurtleEngine.Council;

/// <summary>
/// Builds a smaller projection of the Roslyn payload for the LLM prompt view only.
/// The Turtle Shell guard MUST keep receiving the FULL payload: a model can only
/// cite symbols it is shown, and every shown symbol is a subset of the full
/// allow-list, so trimming cannot strip a valid finding nor admit an ungrounded one.
/// Never mutates the input; rebuilds the immutable records into a new payload.
/// </summary>
public static class PayloadTrimmer
{
    public static RoslynPayload TrimForPrompt(RoslynPayload full, CouncilOptions opts)
    {
        var files = full.Files
            .Take(opts.MaxFiles)
            .Select(file => new FileFacts(
                file.FilePath,
                file.Methods
                    .Take(opts.MaxMethodsPerFile)
                    .Select(method => new MethodFacts(
                        method.Method,
                        method.Allocations.Take(opts.MaxAllocationsPerMethod).ToList(),
                        method.AsyncHealth,
                        method.Dependencies,
                        method.ResolvedSymbols.Take(opts.MaxSymbolsPerMethod).ToList()))
                    .ToList()))
            .ToList();

        return new RoslynPayload(full.RepoSlug, full.DiffBaseline, files);
    }
}
