/**
 * TypeScript models mapping 1:1 to CodeTurtleEngine backend models:
 * - CodeTurtleEngine.Core (Severity, PersonaRole, ReviewFinding, CouncilVerdict, GuardResult)
 * - CodeTurtleEngine.Web.Models.SseEvents (SseEvent, PhaseEventData, etc.)
 * - CodeTurtleEngine.Web.Models.PrModels (GitHubPrRequest, ParsedPrUrl)
 */

export type Severity = 'Info' | 'Nit' | 'Warning' | 'Error' | 'Critical';

export type PersonaRole =
  | 'AllocationsPerformance'
  | 'SecurityAuditor'
  | 'IdiomaticArchitect'
  | 'Arbiter';

export type GuardMode = 'Strip' | 'Flag';

export interface ReviewFinding {
  persona: PersonaRole | string;
  severity: Severity | string;
  title: string;
  detail: string;
  location: string;
  citedSymbolFqns: string[];
}

export interface PersonaVerdict {
  persona: PersonaRole | string;
  findings: ReviewFinding[];
}

export interface GuardResult {
  citedFqn: string;
  verified: boolean;
}

export interface CouncilVerdict {
  personas: PersonaVerdict[];
  synthesized: ReviewFinding[];
  guardAudit: GuardResult[];
}

export interface GitHubPrRequest {
  prUrl: string;
}

export interface ParsedPrUrl {
  owner: string;
  repo: string;
  pullNumber: number;
}

export interface ReviewJobResponse {
  jobId: string;
  estimatedDurationSeconds: number;
}

export interface ReviewResult {
  status?: string;
  markdown: string;
  verdict: CouncilVerdict;
  repoPath?: string;
  stats?: Record<string, unknown>;
}

export const SseEventTypes = {
  Phase: 'phase',
  PersonaStarted: 'persona_started',
  PersonaFinding: 'persona_finding',
  PersonaCompleted: 'persona_completed',
  PersonaFailed: 'persona_failed',
  ArbiterMerge: 'arbiter_merge',
  GuardAudit: 'guard_audit',
  Completed: 'completed',
  Error: 'error',
} as const;

export type SseEventType = (typeof SseEventTypes)[keyof typeof SseEventTypes];

export interface PhaseEventData {
  type: typeof SseEventTypes.Phase;
  phase: string;
  message: string;
  progress?: number | null;
  stats?: Record<string, unknown> | null;
}

export interface PersonaStartedEventData {
  type: typeof SseEventTypes.PersonaStarted;
  persona: string;
  character: string;
}

export interface PersonaFindingEventData {
  type: typeof SseEventTypes.PersonaFinding;
  persona: string;
  character: string;
  finding: ReviewFinding;
}

export interface PersonaCompletedEventData {
  type: typeof SseEventTypes.PersonaCompleted;
  persona: string;
  character: string;
  findingCount: number;
  quip?: string | null;
}

export interface PersonaFailedEventData {
  type: typeof SseEventTypes.PersonaFailed;
  persona: string;
  character: string;
  message: string;
}

export interface ArbiterMergeEventData {
  type: typeof SseEventTypes.ArbiterMerge;
  character: string;
  totalRaw: number;
  afterDedupe: number;
  quip?: string | null;
}

export interface GuardAuditEventData {
  type: typeof SseEventTypes.GuardAudit;
  character: string;
  verified: number;
  stripped: number;
  total: number;
  auditDetails: GuardResult[];
}

export interface CompletedEventData {
  type: typeof SseEventTypes.Completed;
  markdown: string;
  verdict: CouncilVerdict;
  stats?: {
    personasSucceeded?: number;
    findingsKept?: number;
    findingsStripped?: number;
    duration?: number;
    [key: string]: unknown;
  } | null;
}

export interface ErrorEventData {
  type: typeof SseEventTypes.Error;
  code: string;
  message: string;
  funny?: string | null;
}

export type SseEventData =
  | PhaseEventData
  | PersonaStartedEventData
  | PersonaFindingEventData
  | PersonaCompletedEventData
  | PersonaFailedEventData
  | ArbiterMergeEventData
  | GuardAuditEventData
  | CompletedEventData
  | ErrorEventData;

export interface SseEnvelope<T = SseEventData> {
  type: string;
  data: T;
}

export interface ReplayEventItem {
  delay: number;
  event: SseEventData;
}
