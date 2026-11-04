namespace CodeTurtleEngine.Core;

/// <summary>Counts of extracted contexts actually submitted to model prompts, not defect coverage.</summary>
public sealed record PromptCoverage(int ExtractedFiles, int SubmittedFiles, int OmittedFiles,
    int ExtractedMethods, int SubmittedMethods, int OmittedMethods);
