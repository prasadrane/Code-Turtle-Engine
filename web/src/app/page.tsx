'use client';

import React, { useRef, useState } from 'react';
import { HeroHeader } from '@/components/HeroHeader';
import { SiteHeader } from '@/components/SiteHeader';
import { ProductFeatures, WorkflowStrip, SiteFooter } from '@/components/ProductStory';
import { ReviewEntry, LOCAL_SETUP_GUIDE } from '@/components/ReviewEntry';
import { ReviewWorkspace } from '@/components/ReviewWorkspace';
import { LanguageRejectionModal } from '@/components/LanguageRejectionModal';
import { useDemoReplay } from '@/hooks/useDemoReplay';
import { useReviewStream } from '@/hooks/useReviewStream';
import { workspaceReplay, workspaceChanges } from '@/data/workspace-demo';
import { RotateCcw, FastForward, Play, Pause } from 'lucide-react';

export default function HomePage() {
  const [mode, setMode] = useState<'idle' | 'demo' | 'live'>('idle');
  const [activePrUrl, setActivePrUrl] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [speed, setSpeed] = useState(1);
  const workspaceRef = useRef<HTMLElement>(null);
  const entryRef = useRef<HTMLDivElement>(null);
  const demo = useDemoReplay({ events: workspaceReplay });
  const live = useReviewStream({ accessToken });
  const startDemo = () => {
    live.reset(); demo.reset(); setSpeed(1); demo.setSpeed(1); setMode('demo'); demo.start();
    workspaceRef.current?.scrollIntoView?.({ behavior: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
  };
  const startLive = async (url: string) => { demo.reset(); setActivePrUrl(url); setMode('live'); await live.startReview(url); };
  const reset = () => { demo.reset(); live.reset(); setMode('idle'); requestAnimationFrame(() => entryRef.current?.querySelector<HTMLInputElement>('input[type="url"]')?.focus()); };
  const isDemo = mode === 'demo';
  const completed = isDemo ? demo.isCompleted : live.isCompleted;
  const error = isDemo ? demo.error?.message : typeof live.error === 'string' ? live.error : live.error?.message;
  return <>
    <a href="#workspace" className="skip-link">Skip to review workspace</a><SiteHeader />
    <main><HeroHeader onTryDemo={startDemo} /><WorkflowStrip /><ProductFeatures />
      <section id="workspace" ref={workspaceRef} className={`workspace-section ${mode !== 'idle' ? 'workspace-active' : ''}`} aria-labelledby="workspace-title"><div className="page-width">
        <div className="section-heading workspace-heading"><div><span className="eyebrow">YOUR NEXT PULL REQUEST, WITH PERSPECTIVE.</span><h2 id="workspace-title">Give your code a second look.</h2></div>
          <span className="beta-label"><span className="status-dot" /> C# / .NET · Self-hosted beta</span></div>
        {mode === 'idle' ? <div ref={entryRef}><ReviewEntry onSample={startDemo} onSubmit={startLive} initialUrl={activePrUrl} accessToken={accessToken} setAccessToken={setAccessToken} connectionError={live.connectionError} /></div>
          : <ReviewWorkspace key={mode} status={isDemo ? demo.error ? 'failed' : completed ? 'completed' : 'running' : live.status} isDemo={isDemo}
            findings={isDemo ? demo.findings : live.findings} verdict={isDemo ? demo.verdict : live.verdict} audit={isDemo ? demo.guardAudit : live.guardAudit}
            personas={isDemo ? demo.personaStates : live.personas} phase={isDemo ? demo.currentPhase : live.phase} message={isDemo ? demo.phaseMessage : live.phaseMessage}
            markdown={isDemo ? demo.completedMarkdown : live.completedMarkdown} changes={isDemo ? workspaceChanges : live.changes} changesError={live.changesError}
            stats={isDemo ? { personasSucceeded: 3 } : live.stats} error={error} prUrl={activePrUrl} hasStarted={isDemo || live.hasStarted}
            reportOrigin={isDemo ? 'illustrative' : 'live'} onRetry={isDemo ? startDemo : () => { void startLive(activePrUrl); }} onSample={startDemo}
            controls={<>{isDemo && !completed && <><button onClick={() => demo.isPlaying ? demo.pause() : demo.resume()}>{demo.isPlaying ? <Pause size={14} /> : <Play size={14} />}{demo.isPlaying ? 'Pause' : 'Resume'}</button>
              <button onClick={demo.skipToEnd}><FastForward size={14} />Skip to End</button><div className="speed-controls" aria-label="Replay speed">{[1, 2, 5].map(value => <button key={value} aria-pressed={speed === value} onClick={() => { setSpeed(value); demo.setSpeed(value); }}>{value}x</button>)}</div></>}
              {!isDemo && live.isStreaming && <button onClick={live.cancel}>Cancel Review</button>}<button onClick={reset}><RotateCcw size={14} />{completed ? 'Review Another PR' : 'Back to setup'}</button></>} />}
        <p className="workspace-footnote">Compiler facts inform the review. Citation checks establish symbol-name existence; recommendations still need your judgment.</p>
        {mode !== 'idle' && !isDemo && <details className="connection-recovery" id="connection-help"><summary>Server connection help</summary><p>Check that your local server is running, your access key is valid, and the server allows this app’s origin and repository. Use the same hostname for the app and server. Select Back to setup to change your key.</p><a className="text-link" href={LOCAL_SETUP_GUIDE}>Local setup guide</a></details>}
      </div></section>
    </main><SiteFooter /><LanguageRejectionModal isOpen={Boolean(live.rejection?.isOpen)} type={live.rejection?.type}
      detectedLanguage={live.rejection?.detectedLanguage} customMessage={live.rejection?.customMessage} onClose={live.clearRejection} onRetry={startDemo} />
  </>;
}
