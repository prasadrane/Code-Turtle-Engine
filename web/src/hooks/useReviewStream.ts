import { useState, useRef, useCallback } from 'react';
import type {
  ReviewFinding,
  GuardResult,
  CouncilVerdict,
  ErrorEventData,
  PhaseEventData,
  PersonaStartedEventData,
  PersonaFindingEventData,
  PersonaCompletedEventData,
  PersonaFailedEventData,
  GuardAuditEventData,
  CompletedEventData,
} from '../lib/types';
import type { PersonaState } from './useDemoReplay';

export interface UseReviewStreamOptions {
  apiBase?: string;
}

export interface RejectionState {
  isOpen: boolean;
  type?: 'not_dotnet' | 'private_repo' | 'unknown_error' | string;
  detectedLanguage?: string;
  customMessage?: string;
}

export interface UseReviewStreamReturn {
  isStreaming: boolean;
  isCompleted: boolean;
  phase: string;
  currentPhase: string;
  phaseMessage: string;
  personas: Record<string, PersonaState>;
  personaStates: Record<string, PersonaState>;
  findings: ReviewFinding[];
  auditSymbols: GuardResult[];
  guardAudit: GuardResult[];
  finalVerdict: CouncilVerdict | null;
  verdict: CouncilVerdict | null;
  completedMarkdown: string | null;
  markdown: string | null;
  rejection: RejectionState | null;
  error: ErrorEventData | string | null;
  startReview: (prUrl: string) => Promise<void>;
  cancel: () => void;
  reset: () => void;
  clearRejection: () => void;
}

const initialPersonaStates: Record<string, PersonaState> = {
  AllocationsPerformance: { status: 'idle', findingCount: 0 },
  SecurityAuditor: { status: 'idle', findingCount: 0 },
  IdiomaticArchitect: { status: 'idle', findingCount: 0 },
};

const SSE_EVENTS = [
  'phase', 'stage', 'persona_started', 'persona_quip', 'persona_finding',
  'finding_stream', 'persona_completed', 'persona_failed', 'guard_started',
  'symbol_checked', 'guard_audit', 'arbiter_merge', 'verdict', 'completed', 'error',
];

export function useReviewStream(options: UseReviewStreamOptions = {}): UseReviewStreamReturn {
  const apiBase = options.apiBase ?? (process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5000');

  const [isStreaming, setIsStreaming] = useState(false);
  const [isCompleted, setIsCompleted] = useState(false);
  const [phase, setPhase] = useState('');
  const [phaseMessage, setPhaseMessage] = useState('');
  const [personas, setPersonas] = useState<Record<string, PersonaState>>(initialPersonaStates);
  const [findings, setFindings] = useState<ReviewFinding[]>([]);
  const [auditSymbols, setAuditSymbols] = useState<GuardResult[]>([]);
  const [finalVerdict, setFinalVerdict] = useState<CouncilVerdict | null>(null);
  const [completedMarkdown, setCompletedMarkdown] = useState<string | null>(null);
  const [rejection, setRejection] = useState<RejectionState | null>(null);
  const [error, setError] = useState<ErrorEventData | string | null>(null);

  const eventSourceRef = useRef<EventSource | null>(null);

  const closeStream = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  const clearRejection = useCallback(() => setRejection(null), []);

  const reset = useCallback(() => {
    closeStream();
    setIsStreaming(false);
    setIsCompleted(false);
    setPhase('');
    setPhaseMessage('');
    setPersonas(initialPersonaStates);
    setFindings([]);
    setAuditSymbols([]);
    setFinalVerdict(null);
    setCompletedMarkdown(null);
    setRejection(null);
    setError(null);
  }, [closeStream]);

  const cancel = useCallback(() => {
    closeStream();
    setIsStreaming(false);
    setPhaseMessage('Review cancelled.');
  }, [closeStream]);

  const handleSseMessage = useCallback((eventType: string, dataStr: string) => {
    try {
      const data = JSON.parse(dataStr);
      switch (eventType) {
        case 'phase':
        case 'stage': {
          const pd = data as PhaseEventData;
          setPhase(pd.phase);
          setPhaseMessage(pd.message);
          break;
        }
        case 'persona_started': {
          const sd = data as PersonaStartedEventData;
          setPersonas((prev) => ({ ...prev, [sd.persona]: { status: 'running', findingCount: 0 } }));
          break;
        }
        case 'persona_quip': {
          const p = data.persona || data.character;
          if (p) {
            setPersonas((prev) => ({ ...prev, [p]: { ...(prev[p] || { status: 'running', findingCount: 0 }), quip: data.quip } }));
          }
          break;
        }
        case 'persona_finding':
        case 'finding_stream': {
          const fd = data as PersonaFindingEventData;
          if (fd.finding) setFindings((prev) => [...prev, fd.finding]);
          break;
        }
        case 'persona_completed': {
          const cd = data as PersonaCompletedEventData;
          setPersonas((prev) => ({
            ...prev,
            [cd.persona]: { status: 'completed', findingCount: cd.findingCount ?? 0, quip: cd.quip ?? undefined },
          }));
          break;
        }
        case 'persona_failed': {
          const fail = data as PersonaFailedEventData;
          setPersonas((prev) => ({ ...prev, [fail.persona]: { status: 'failed', findingCount: 0, quip: fail.message } }));
          break;
        }
        case 'guard_started': {
          setPhase('auditing');
          setPhaseMessage(data.message || 'Judge Shellsworth verifying Roslyn symbol citations...');
          break;
        }
        case 'symbol_checked': {
          if (data.citedFqn !== undefined) setAuditSymbols((prev) => [...prev, data as GuardResult]);
          break;
        }
        case 'guard_audit': {
          const ad = data as GuardAuditEventData;
          if (ad.auditDetails) setAuditSymbols(ad.auditDetails);
          break;
        }
        case 'verdict':
        case 'completed': {
          const comp = data as CompletedEventData;
          setCompletedMarkdown(comp.markdown);
          setFinalVerdict(comp.verdict);
          setIsCompleted(true);
          setIsStreaming(false);
          closeStream();
          break;
        }
        case 'error': {
          const err = data as ErrorEventData;
          setError(err);
          if (err.code === 'NOT_DOTNET' || err.code === 'NOT_ACCESSIBLE') {
            setRejection({
              isOpen: true,
              type: err.code === 'NOT_DOTNET' ? 'not_dotnet' : 'private_repo',
              detectedLanguage: err.message?.includes('Python') ? 'python' : undefined,
              customMessage: err.funny || err.message,
            });
          }
          setIsStreaming(false);
          closeStream();
          break;
        }
      }
    } catch {
      // Ignore unparseable SSE data chunk
    }
  }, [closeStream]);

  const startReview = useCallback(
    async (prUrl: string) => {
      reset();
      setIsStreaming(true);
      setPhase('validating');
      setPhaseMessage('Submitting pull request to Code-Turtle Council...');

      try {
        const response = await fetch(`${apiBase}/api/review`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ prUrl }),
        });

        if (!response.ok) {
          const err = await response.json().catch(() => null);
          const isNonDotNet = err?.code === 'NOT_DOTNET' || err?.type === 'not_dotnet' || (err?.error && /non-?\.net|c#/i.test(err.error));
          const isPrivate = err?.code === 'NOT_ACCESSIBLE' || err?.type === 'private_repo' || (err?.error && /private/i.test(err.error));

          if (isNonDotNet || isPrivate) {
            setRejection({
              isOpen: true,
              type: isPrivate ? 'private_repo' : 'not_dotnet',
              detectedLanguage: err?.language || err?.detectedLanguage,
              customMessage: err?.funny || err?.error || err?.message,
            });
          } else {
            setError(err?.error || `Server responded with status ${response.status}`);
          }
          setIsStreaming(false);
          return;
        }

        const data = await response.json();
        const streamPath = data.streamUrl || `/api/review/${data.jobId}/stream`;
        const fullStreamUrl = streamPath.startsWith('http') ? streamPath : `${apiBase}${streamPath}`;

        const es = new EventSource(fullStreamUrl);
        eventSourceRef.current = es;

        SSE_EVENTS.forEach((evt) => {
          es.addEventListener(evt, (e: MessageEvent) => handleSseMessage(evt, e.data));
        });

        es.onerror = () => {
          if (!isCompleted) {
            setError('Connection to review stream closed.');
            setIsStreaming(false);
            closeStream();
          }
        };
      } catch (err: any) {
        setError(err?.message || 'Network error occurred.');
        setIsStreaming(false);
      }
    },
    [apiBase, reset, handleSseMessage, isCompleted, closeStream]
  );

  return {
    isStreaming,
    isCompleted,
    phase,
    currentPhase: phase,
    phaseMessage,
    personas,
    personaStates: personas,
    findings,
    auditSymbols,
    guardAudit: auditSymbols,
    finalVerdict,
    verdict: finalVerdict,
    completedMarkdown,
    markdown: completedMarkdown,
    rejection,
    error,
    startReview,
    cancel,
    reset,
    clearRejection,
  };
}
