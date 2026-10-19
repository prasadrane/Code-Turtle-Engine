namespace CodeTurtleEngine.Web.Models;

// Browser-only display contract. Never include this data in RoslynPayload or prompts.
public sealed record ReviewSnapshot(string Repository, int PullNumber, string Title,
    string BaseSha, string HeadSha, string? MergeBaseSha, string ComparisonKind);

public sealed record DiffLine(string Kind, int? OldLine, int? NewLine, string Text);
public sealed record DiffHunk(string Header, IReadOnlyList<DiffLine> Lines);
public sealed record ChangedFile(string Path, string? OldPath, string Status, bool Analyzed,
    string? Source, IReadOnlyList<DiffHunk> Hunks, string? UnavailableReason = null);
// FindingIndex refers only to the completed CouncilVerdict.Synthesized ordering.
public sealed record FindingAnchor(int FindingIndex, string Path, int StartLine, int EndLine, string Provenance);
public sealed record ReviewChanges(int SchemaVersion, ReviewSnapshot Snapshot,
    IReadOnlyList<ChangedFile> Files, IReadOnlyList<FindingAnchor> Anchors, string? UnavailableReason = null);
