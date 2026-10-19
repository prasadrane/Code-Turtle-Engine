import type { CouncilVerdict, ErrorEventData, GuardResult, ReviewFinding } from './types';
import type { ReviewChanges, ReviewStatus } from './changes';
import type { PersonaState } from '@/hooks/useDemoReplay';

export interface LiveReviewState {
  status: ReviewStatus; hasStarted: boolean; phase: string; phaseMessage: string;
  personas: Record<string, PersonaState>; findings: ReviewFinding[]; auditSymbols: GuardResult[];
  finalVerdict: CouncilVerdict | null; completedMarkdown: string | null; stats: Record<string, unknown> | null;
  rejection: { isOpen: boolean; type?: string; detectedLanguage?: string; customMessage?: string } | null;
  error: ErrorEventData | string | null; changes: ReviewChanges | null; changesError: string | null;
}
export const initialReviewState: LiveReviewState = {
  status: 'idle', hasStarted: false, phase: '', phaseMessage: '',
  personas: { AllocationsPerformance: { status: 'idle', findingCount: 0 }, SecurityAuditor: { status: 'idle', findingCount: 0 }, IdiomaticArchitect: { status: 'idle', findingCount: 0 } },
  findings: [], auditSymbols: [], finalVerdict: null, completedMarkdown: null, rejection: null, error: null, changes: null, changesError: null, stats: null,
};
export type ReviewAction = { type: 'reset' } | { type: 'set'; value: Partial<LiveReviewState> } | { type: 'event'; event: string; data: Record<string, any> };
export function reviewReducer(state: LiveReviewState, action: ReviewAction): LiveReviewState {
  if (action.type === 'reset') return initialReviewState;
  if (action.type === 'set') return { ...state, ...action.value };
  const data = action.data;
  switch (action.event) {
    case 'phase': case 'stage': return { ...state, status: 'running', phase: data.phase, phaseMessage: data.message };
    case 'persona_started': return { ...state, status: 'running', personas: { ...state.personas, [data.persona]: { status: 'running', findingCount: 0 } } };
    case 'persona_quip': return { ...state, personas: { ...state.personas, [data.persona || data.character]: { ...state.personas[data.persona || data.character], quip: data.quip } } };
    case 'persona_finding': case 'finding_stream': return data.finding ? { ...state, findings: [...state.findings, data.finding] } : state;
    case 'persona_completed': return { ...state, personas: { ...state.personas, [data.persona]: { status: 'completed', findingCount: data.findingCount ?? 0, quip: data.quip } } };
    case 'persona_failed': return { ...state, personas: { ...state.personas, [data.persona]: { status: 'failed', findingCount: 0, quip: data.message } } };
    case 'guard_started': return { ...state, phase: 'auditing', phaseMessage: 'Checking citation evidence…' };
    case 'symbol_checked': return data.citedFqn ? { ...state, auditSymbols: [...state.auditSymbols, data as GuardResult] } : state;
    case 'guard_audit': return { ...state, auditSymbols: data.auditDetails ?? [] };
    case 'verdict': case 'completed': return { ...state, status: 'completed', phase: 'completed', completedMarkdown: data.markdown,
      finalVerdict: data.verdict, findings: data.verdict.synthesized, auditSymbols: data.verdict.guardAudit, stats: data.stats ?? null };
    case 'error': return { ...state, status: 'failed', error: data as ErrorEventData, rejection: ['NOT_DOTNET', 'NOT_ACCESSIBLE'].includes(data.code)
      ? { isOpen: true, type: data.code === 'NOT_DOTNET' ? 'not_dotnet' : 'private_repo', customMessage: data.message } : null };
    default: return state;
  }
}
