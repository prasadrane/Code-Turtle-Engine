'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import type { MouseEvent } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { FastForward, Pause, Play } from 'lucide-react';
import { AppBar } from './AppBar';
import { StatusBar, CITATION_LIMITS } from './StatusBar';
import { ReviewEntry, LOCAL_SETUP_GUIDE } from './ReviewEntry';
import { ReviewWorkspace } from './ReviewWorkspace';
import { LanguageRejectionModal } from './LanguageRejectionModal';
import { LeaveReviewDialog } from './LeaveReviewDialog';
import { useDemoReplay } from '@/hooks/useDemoReplay';
import { useReviewStream } from '@/hooks/useReviewStream';
import { workspaceChanges, workspaceReplay } from '@/data/workspace-demo';

export function ReviewApp() {
  const requestedSample = useSearchParams().get('sample') === '1';
  const router = useRouter();
  const [sample, setSample] = useState(requestedSample);
  const [revision, setRevision] = useState(0);
  const [focusVersion, setFocusVersion] = useState(0);
  useEffect(() => { setSample(requestedSample); }, [requestedSample]);
  const openSample = () => {
    setSample(true);
    setRevision(value => value + 1);
    router.push('/review?sample=1');
  };
  const resetIntent = () => {
    setSample(false);
    setFocusVersion(value => value + 1);
    router.replace('/review');
  };
  return <ReviewSession key={`${sample ? 'sample' : 'entry'}:${revision}`} sample={sample}
    focusVersion={focusVersion} onSample={openSample} onReset={resetIntent} />;
}

function ReviewSession({ sample, focusVersion, onSample, onReset }: {
  sample: boolean; focusVersion: number; onSample: () => void; onReset: () => void;
}) {
  const router = useRouter();
  const [mode, setMode] = useState<'idle' | 'demo' | 'live'>(sample ? 'demo' : 'idle');
  const [activePrUrl, setActivePrUrl] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [speed, setSpeed] = useState(1);
  const [leaving, setLeaving] = useState(false);
  const pendingAction = useRef<(() => void) | null>(null);
  const entryRef = useRef<HTMLDivElement>(null);
  // Keyed sessions make URL re-entry fresh. Existing autoStart survives Strict Mode cleanup/setup.
  const demo = useDemoReplay({ events: workspaceReplay, autoStart: sample });
  const live = useReviewStream({ accessToken });
  const guarded = mode === 'live' && live.isStreaming;
  const isDemo = mode === 'demo';
  const completed = isDemo ? demo.isCompleted : live.isCompleted;
  const error = isDemo ? demo.error?.message : typeof live.error === 'string' ? live.error : live.error?.message;
  const status = isDemo ? demo.error ? 'failed' : completed ? 'completed' : 'running' : live.status;
  const changes = isDemo ? workspaceChanges : live.changes;
  const personas = isDemo ? demo.personaStates : live.personas;
  const phase = isDemo ? demo.currentPhase : live.phase;
  const message = isDemo ? demo.phaseMessage : live.phaseMessage;
  const hasConfiguredAddress = Boolean(process.env.NEXT_PUBLIC_API_BASE?.trim()) || process.env.NODE_ENV !== 'production';
  const connectionState = live.connectionError ? hasConfiguredAddress ? 'issue' : 'unconfigured' : 'configured';
  useEffect(() => {
    if (!guarded) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [guarded]);
  useEffect(() => {
    if (!focusVersion || mode !== 'idle') return;
    const frame = requestAnimationFrame(() => {
      const target = entryRef.current?.querySelector<HTMLElement>('input[type="url"]:not(:disabled)')
        ?? entryRef.current?.querySelector<HTMLElement>('[data-launcher-heading]');
      target?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [focusVersion, mode]);
  const requestAction = (action: () => void) => {
    if (!guarded) { action(); return; }
    pendingAction.current = action;
    setLeaving(true);
  };
  const stay = useCallback(() => { pendingAction.current = null; setLeaving(false); }, []);
  const leave = () => {
    const action = pendingAction.current;
    pendingAction.current = null;
    setLeaving(false);
    action?.();
  };
  const reset = () => {
    demo.reset(); live.reset(); setSpeed(1); setMode('idle'); onReset();
  };
  const startSample = () => requestAction(() => { live.reset(); onSample(); });
  const startLive = async (url: string) => {
    demo.reset(); setActivePrUrl(url); setMode('live'); await live.startReview(url);
  };
  const captureLink = (event: MouseEvent<HTMLDivElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const anchor = (event.target as Element).closest<HTMLAnchorElement>('a[href]');
    if (!anchor || anchor.target === '_blank' || anchor.hasAttribute('download')) return;
    const destination = new URL(anchor.href, window.location.href);
    const sameOrigin = destination.origin === window.location.origin;
    if (sameOrigin && destination.pathname === '/review' && !destination.search) return;
    if (!guarded && !sameOrigin) return;
    event.preventDefault();
    event.stopPropagation();
    requestAction(() => {
      if (guarded) live.reset();
      if (sameOrigin) router.push(`${destination.pathname}${destination.search}${destination.hash}`);
      else window.location.assign(destination.href);
    });
  };
  const replayControls = isDemo ? !completed ? <>
    <button type="button" onClick={() => demo.isPlaying ? demo.pause() : demo.resume()}>
      {demo.isPlaying ? <Pause size={14} /> : <Play size={14} />}{demo.isPlaying ? 'Pause' : 'Resume'}</button>
    <button type="button" onClick={demo.skipToEnd}><FastForward size={14} />Skip to End</button>
    <div className="speed-controls" aria-label="Replay speed">{[1, 2, 5].map(value => <button key={value}
      aria-pressed={speed === value} onClick={() => { setSpeed(value); demo.setSpeed(value); }}>{value}x</button>)}</div>
  </> : <button type="button" onClick={startSample}><Play size={14} />Replay sample</button> : undefined;
  return <div className="review-app" onClickCapture={captureLink}>
    <AppBar active={mode !== 'idle'} isDemo={isDemo} status={mode === 'idle' ? 'idle' : status}
      hasStarted={isDemo || live.hasStarted} snapshot={mode === 'idle' ? undefined : changes?.snapshot}
      prUrl={activePrUrl} isStreaming={live.isStreaming} onCancel={live.cancel} onNewReview={() => requestAction(reset)} />
    <main id="review-main" tabIndex={-1} className={`review-main review-main-${mode === 'idle' ? 'idle' : 'active'}`}>
      {mode === 'idle' ? <div ref={entryRef} className="review-launcher">
        <h1 data-launcher-heading tabIndex={-1}>Start a review</h1>
        <ReviewEntry onSample={startSample} onSubmit={startLive} initialUrl={activePrUrl}
          accessToken={accessToken} setAccessToken={setAccessToken} connectionError={live.connectionError} connectionState={connectionState} />
        <p className="workspace-footnote">{CITATION_LIMITS}</p>
      </div> : <ReviewWorkspace key={mode} status={status} showChrome={false}
        isDemo={isDemo} findings={isDemo ? demo.findings : live.findings} verdict={isDemo ? demo.verdict : live.verdict}
        audit={isDemo ? demo.guardAudit : live.guardAudit} personas={personas} phase={phase} message={message}
        markdown={isDemo ? demo.completedMarkdown : live.completedMarkdown} changes={changes}
        changesError={live.changesError} stats={isDemo ? { personasSucceeded: 3 } : live.stats} error={error}
        prUrl={activePrUrl} hasStarted={isDemo || live.hasStarted} reportOrigin={isDemo ? 'illustrative' : 'live'}
        onRetry={isDemo ? startSample : () => { void startLive(activePrUrl); }} onSample={startSample} />}
      {mode === 'live' && <details className="connection-recovery" id="connection-help"><summary>Server connection help</summary>
        <p>Check that your local server is running, your access key is valid, and the server allows this app’s origin and repository.
          Use the same hostname for the app and server. Select New review to change your key.</p>
        <a className="text-link" href={LOCAL_SETUP_GUIDE}>Local setup guide</a>
      </details>}
    </main>
    {mode !== 'idle' && <StatusBar status={status} phase={phase} message={message} personas={personas} replayControls={replayControls} />}
    <LanguageRejectionModal isOpen={Boolean(live.rejection?.isOpen)} type={live.rejection?.type}
      detectedLanguage={live.rejection?.detectedLanguage} customMessage={live.rejection?.customMessage}
      onClose={live.clearRejection} onRetry={startSample} />
    {leaving && <LeaveReviewDialog onStay={stay} onLeave={leave} />}
  </div>;
}
