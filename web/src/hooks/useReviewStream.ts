import { useReducer, useRef, useCallback, useEffect } from 'react';
import { initialReviewState, reviewReducer } from '@/lib/reviewState';
import type { ReviewChanges } from '@/lib/changes';

export interface UseReviewStreamOptions { apiBase?: string; accessToken?: string; }
export interface RejectionState { isOpen: boolean; type?: string; detectedLanguage?: string; customMessage?: string; }
const events = ['phase', 'stage', 'persona_started', 'persona_quip', 'persona_finding', 'finding_stream',
  'persona_completed', 'persona_failed', 'guard_started', 'symbol_checked', 'guard_audit', 'arbiter_merge', 'verdict', 'completed', 'error'];

export function useReviewStream(options: UseReviewStreamOptions = {}) {
  const apiBase = options.apiBase ?? (process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5000');
  const [state, dispatch] = useReducer(reviewReducer, initialReviewState);
  const eventSource = useRef<EventSource | null>(null);
  const activeJob = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const generation = useRef(0);
  const terminal = useRef(false);
  const closeStream = useCallback(() => { eventSource.current?.close(); eventSource.current = null; }, []);
  const cancelJob = useCallback(() => {
    if (activeJob.current) void fetch(`${apiBase}/api/review/${activeJob.current}`, { method: 'DELETE', credentials: 'include' }).catch(() => {});
    activeJob.current = null;
  }, [apiBase]);
  const stop = useCallback(() => {
    generation.current += 1;
    terminal.current = true;
    controller.current?.abort();
    closeStream();
    cancelJob();
  }, [closeStream, cancelJob]);
  const reset = useCallback(() => { stop(); dispatch({ type: 'reset' }); }, [stop]);
  const cancel = useCallback(() => { stop(); dispatch({ type: 'set', value: { status: 'cancelled', phaseMessage: 'Review cancelled.' } }); }, [stop]);
  const clearRejection = useCallback(() => dispatch({ type: 'set', value: { rejection: null } }), []);

  const startReview = useCallback(async (prUrl: string) => {
    reset();
    const current = generation.current;
    terminal.current = false;
    const request = new AbortController();
    controller.current = request;
    dispatch({ type: 'set', value: { status: 'submitting', phase: 'validating', phaseMessage: 'Submitting your pull request…' } });
    const stale = () => current !== generation.current;
    const loadChanges = async (jobId: string) => {
      try {
        const response = await fetch(`${apiBase}/api/review/${jobId}/changes`, { credentials: 'include', cache: 'no-store', signal: request.signal });
        if (stale()) return;
        if (!response.ok || response.status === 202) throw new Error(response.status === 404 ? 'The source artifact expired or this session cannot access it.' : 'The source artifact is not available.');
        const artifact = await response.json();
        if (stale()) return;
        if (artifact.schemaVersion !== 1 || !Array.isArray(artifact.files)) throw new Error(artifact.unavailableReason || 'No supported source artifact was returned.');
        dispatch({ type: 'set', value: { changes: artifact as ReviewChanges } });
      } catch (error) {
        if (!stale()) dispatch({ type: 'set', value: { changesError: error instanceof Error ? error.message : 'Source artifact unavailable.' } });
      }
    };
    try {
      if (options.accessToken) {
        const response = await fetch(`${apiBase}/api/session`, { method: 'POST', credentials: 'include', signal: request.signal,
          headers: { Authorization: `Bearer ${options.accessToken}` } });
        if (stale()) return;
        if (!response.ok) throw new Error(response.status === 503 ? 'Live reviews are disabled on this server. Configure trusted-local access first.' : 'Local beta authentication failed. Check your access key and server origin.');
      }
      const response = await fetch(`${apiBase}/api/review`, { method: 'POST', credentials: 'include', signal: request.signal,
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prUrl }) });
      if (!response.ok) {
        const data = await response.json().catch(() => null);
        if (stale()) return;
        const notDotNet = data?.code === 'NOT_DOTNET' || data?.type === 'not_dotnet';
        const inaccessible = data?.code === 'NOT_ACCESSIBLE' || data?.type === 'private_repo';
        const fallback: Record<number, string> = { 401: 'Your local-server session is missing or expired. Check the access key.',
          403: 'This repository or browser origin is not allowed by your local server.', 429: 'The review queue or request limit is full. Try again shortly.',
          503: 'Live reviews are disabled on this server. Configure trusted-local access first.' };
        dispatch({ type: 'set', value: { status: 'failed', error: data?.error || data?.message || fallback[response.status] || `Server responded with status ${response.status}`,
          rejection: notDotNet || inaccessible ? { isOpen: true, type: inaccessible ? 'private_repo' : 'not_dotnet', detectedLanguage: data?.language || data?.detectedLanguage, customMessage: data?.error || data?.message } : null } });
        return;
      }
      const data = await response.json();
      if (stale()) {
        if (data.jobId) void fetch(`${apiBase}/api/review/${data.jobId}`, { method: 'DELETE', credentials: 'include' }).catch(() => {});
        return;
      }
      if (typeof data.jobId !== 'string') throw new Error('The server did not return a review identifier.');
      activeJob.current = data.jobId;
      dispatch({ type: 'set', value: { status: 'queued', hasStarted: true, phaseMessage: 'Your review is queued.' } });
      const streamPath = data.streamUrl || `/api/review/${data.jobId}/stream`;
      const streamUrl = new URL(streamPath, apiBase);
      if (streamUrl.origin !== new URL(apiBase).origin) throw new Error('The review stream must use the configured server.');
      const source = new EventSource(streamUrl.toString(), { withCredentials: true });
      eventSource.current = source;
      events.forEach(event => source.addEventListener(event, (message: MessageEvent) => {
        if (stale() || terminal.current) return;
        try {
          const eventData = JSON.parse(message.data);
          dispatch({ type: 'event', event, data: eventData });
          if (event === 'completed' || event === 'verdict' || event === 'error') {
            terminal.current = true;
            closeStream();
            if (event !== 'error') void loadChanges(data.jobId);
          }
        } catch { /* Ignore malformed events; never manufacture a completed report. */ }
      }));
      source.onerror = () => {
        if (stale() || terminal.current) return;
        terminal.current = true;
        dispatch({ type: 'set', value: { status: 'failed', error: 'Connection to review stream closed. Results may be incomplete.' } });
        closeStream();
      };
    } catch (error) {
      if (!stale()) dispatch({ type: 'set', value: { status: 'failed', error: error instanceof Error ? error.message : 'Network error occurred.' } });
    }
  }, [apiBase, options.accessToken, reset, closeStream]);
  useEffect(() => () => { stop(); }, [stop]);
  return { ...state, isStreaming: ['submitting', 'queued', 'running'].includes(state.status), isCompleted: state.status === 'completed',
    currentPhase: state.phase, personaStates: state.personas, guardAudit: state.auditSymbols, verdict: state.finalVerdict,
    markdown: state.completedMarkdown, startReview, cancel, reset, clearRejection };
}
export type UseReviewStreamReturn = ReturnType<typeof useReviewStream>;
