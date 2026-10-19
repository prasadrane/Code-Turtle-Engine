/** Browser-only source artifacts; never part of the compiler-fact model payload. */
export interface DiffLine { kind: 'added' | 'removed' | 'context'; oldLine: number | null; newLine: number | null; text: string; }
export interface DiffHunk { header: string; lines: DiffLine[]; }
export interface ChangedFile {
  path: string; oldPath: string | null; status: string; analyzed: boolean;
  source: string | null; hunks: DiffHunk[]; unavailableReason?: string | null;
}
export interface FindingAnchor {
  findingIndex: number; path: string; startLine: number; endLine: number;
  provenance: 'RoslynMember' | 'FixtureLine';
}
export interface ReviewChanges {
  schemaVersion: 1;
  snapshot: { repository: string; pullNumber: number; title: string; baseSha: string;
    headSha: string; mergeBaseSha: string | null; comparisonKind: string; };
  files: ChangedFile[]; anchors: FindingAnchor[]; unavailableReason?: string | null;
}
export type ReviewStatus = 'idle' | 'submitting' | 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';
export type ReportOrigin = 'live' | 'historical' | 'illustrative';
export function reportWithProvenance(markdown: string, origin: ReportOrigin = 'live') {
  const disclosure = origin === 'historical'
    ? '> Historical recorded sample. Original citation evidence is unavailable. Verification claims below are historical and are not current verification. No model calls were made.\n\n'
    : origin === 'illustrative'
      ? '> Code Turtle illustrative sample. Authored findings and fixture source/diff; no model calls were made. This is not a live review or measured model result.\n\n'
      : '> Code Turtle advisory review. Citation existence and source context do not establish recommendation correctness.\n\n';
  return disclosure + markdown;
}
