import { useState, useRef, useCallback, useEffect } from 'react';
import defaultReplayData from '../data/demo-replay.json';
import type {
  ReplayEventItem,
  SseEventData,
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

export interface PersonaState {
  status: 'idle' | 'running' | 'completed' | 'failed';
  findingCount: number;
  quip?: string;
}

export interface UseDemoReplayOptions {
  events?: ReplayEventItem[];
  autoStart?: boolean;
  speedMultiplier?: number;
  onEvent?: (event: SseEventData) => void;
  onComplete?: () => void;
}

export interface UseDemoReplayReturn {
  isPlaying: boolean;
  isPaused: boolean;
  isCompleted: boolean;
  currentEvent: SseEventData | null;
  events: SseEventData[];
  currentIndex: number;
  totalEvents: number;
  progress: number;
  currentPhase: string;
  phaseMessage: string;
  personaStates: Record<string, PersonaState>;
  findings: ReviewFinding[];
  guardAudit: GuardResult[];
  verdict: CouncilVerdict | null;
  completedMarkdown: string | null;
  error: ErrorEventData | null;
  start: () => void;
  pause: () => void;
  resume: () => void;
  reset: () => void;
  skipToEnd: () => void;
  setSpeed: (multiplier: number) => void;
}

const initialPersonaStates: Record<string, PersonaState> = {
  AllocationsPerformance: { status: 'idle', findingCount: 0 },
  SecurityAuditor: { status: 'idle', findingCount: 0 },
  IdiomaticArchitect: { status: 'idle', findingCount: 0 },
};

export function useDemoReplay(options: UseDemoReplayOptions = {}): UseDemoReplayReturn {
  const replayItems: ReplayEventItem[] = options.events ?? (defaultReplayData as ReplayEventItem[]);
  const [speed, setSpeed] = useState(options.speedMultiplier ?? 1);
  const [isPlaying, setIsPlaying] = useState(false);
  const [isPaused, setIsPaused] = useState(false);
  const [isCompleted, setIsCompleted] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [currentEvent, setCurrentEvent] = useState<SseEventData | null>(null);
  const [processedEvents, setProcessedEvents] = useState<SseEventData[]>([]);

  const [currentPhase, setCurrentPhase] = useState<string>('');
  const [phaseMessage, setPhaseMessage] = useState<string>('');
  const [personaStates, setPersonaStates] = useState<Record<string, PersonaState>>(initialPersonaStates);
  const [findings, setFindings] = useState<ReviewFinding[]>([]);
  const [guardAudit, setGuardAudit] = useState<GuardResult[]>([]);
  const [verdict, setVerdict] = useState<CouncilVerdict | null>(null);
  const [completedMarkdown, setCompletedMarkdown] = useState<string | null>(null);
  const [error, setError] = useState<ErrorEventData | null>(null);

  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const indexRef = useRef(0);
  const isPlayingRef = useRef(false);
  const isPausedRef = useRef(false);
  const speedRef = useRef(speed);
  speedRef.current = speed;

  const applyEvent = useCallback((event: SseEventData) => {
    setCurrentEvent(event);
    setProcessedEvents((prev) => [...prev, event]);
    options.onEvent?.(event);

    switch (event.type) {
      case 'phase': {
        const data = event as PhaseEventData;
        setCurrentPhase(data.phase);
        setPhaseMessage(data.message);
        break;
      }
      case 'persona_started': {
        const data = event as PersonaStartedEventData;
        setPersonaStates((prev) => ({
          ...prev,
          [data.persona]: { status: 'running', findingCount: 0 },
        }));
        break;
      }
      case 'persona_finding': {
        const data = event as PersonaFindingEventData;
        setFindings((prev) => [...prev, data.finding]);
        break;
      }
      case 'persona_completed': {
        const data = event as PersonaCompletedEventData;
        setPersonaStates((prev) => ({
          ...prev,
          [data.persona]: {
            status: 'completed',
            findingCount: data.findingCount,
            quip: data.quip ?? undefined,
          },
        }));
        break;
      }
      case 'persona_failed': {
        const data = event as PersonaFailedEventData;
        setPersonaStates((prev) => ({
          ...prev,
          [data.persona]: {
            status: 'failed',
            findingCount: 0,
            quip: data.message,
          },
        }));
        break;
      }
      case 'guard_audit': {
        const data = event as GuardAuditEventData;
        setGuardAudit(data.auditDetails);
        break;
      }
      case 'completed': {
        const data = event as CompletedEventData;
        setCompletedMarkdown(data.markdown);
        setVerdict(data.verdict);
        setIsCompleted(true);
        setIsPlaying(false);
        isPlayingRef.current = false;
        options.onComplete?.();
        break;
      }
      case 'error': {
        const data = event as ErrorEventData;
        setError(data);
        setIsPlaying(false);
        isPlayingRef.current = false;
        break;
      }
    }
  }, [options]);

  const clearTimer = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const scheduleNext = useCallback((nextIndex: number) => {
    if (nextIndex >= replayItems.length) {
      setIsCompleted(true);
      setIsPlaying(false);
      isPlayingRef.current = false;
      options.onComplete?.();
      return;
    }

    const currentDelay = nextIndex === 0 ? 0 : replayItems[nextIndex - 1].delay;
    const targetDelay = replayItems[nextIndex].delay;
    const deltaMs = Math.max(0, (targetDelay - currentDelay) / (speedRef.current || 1));

    timerRef.current = setTimeout(() => {
      if (!isPlayingRef.current || isPausedRef.current) return;
      applyEvent(replayItems[nextIndex].event);
      indexRef.current = nextIndex + 1;
      setCurrentIndex(nextIndex + 1);
      scheduleNext(nextIndex + 1);
    }, deltaMs);
  }, [replayItems, applyEvent, options]);

  const start = useCallback(() => {
    clearTimer();
    setIsPlaying(true);
    setIsPaused(false);
    isPlayingRef.current = true;
    isPausedRef.current = false;
    indexRef.current = 0;
    setCurrentIndex(0);
    scheduleNext(0);
  }, [clearTimer, scheduleNext]);

  const pause = useCallback(() => {
    clearTimer();
    setIsPlaying(false);
    setIsPaused(true);
    isPlayingRef.current = false;
    isPausedRef.current = true;
  }, [clearTimer]);

  const resume = useCallback(() => {
    if (!isPausedRef.current) return;
    setIsPlaying(true);
    setIsPaused(false);
    isPlayingRef.current = true;
    isPausedRef.current = false;
    scheduleNext(indexRef.current);
  }, [scheduleNext]);

  const reset = useCallback(() => {
    clearTimer();
    setIsPlaying(false);
    setIsPaused(false);
    setIsCompleted(false);
    isPlayingRef.current = false;
    isPausedRef.current = false;
    indexRef.current = 0;
    setCurrentIndex(0);
    setCurrentEvent(null);
    setProcessedEvents([]);
    setCurrentPhase('');
    setPhaseMessage('');
    setPersonaStates(initialPersonaStates);
    setFindings([]);
    setGuardAudit([]);
    setVerdict(null);
    setCompletedMarkdown(null);
    setError(null);
  }, [clearTimer]);

  const skipToEnd = useCallback(() => {
    clearTimer();
    for (let i = indexRef.current; i < replayItems.length; i++) {
      applyEvent(replayItems[i].event);
    }
    indexRef.current = replayItems.length;
    setCurrentIndex(replayItems.length);
    setIsCompleted(true);
    setIsPlaying(false);
    isPlayingRef.current = false;
    isPausedRef.current = false;
  }, [clearTimer, replayItems, applyEvent]);

  useEffect(() => {
    if (options.autoStart) {
      start();
    }
    return () => clearTimer();
  }, []);

  const totalEvents = replayItems.length;
  const progress = totalEvents > 0 ? currentIndex / totalEvents : 0;

  return {
    isPlaying,
    isPaused,
    isCompleted,
    currentEvent,
    events: processedEvents,
    currentIndex,
    totalEvents,
    progress,
    currentPhase,
    phaseMessage,
    personaStates,
    findings,
    guardAudit,
    verdict,
    completedMarkdown,
    error,
    start,
    pause,
    resume,
    reset,
    skipToEnd,
    setSpeed,
  };
}
