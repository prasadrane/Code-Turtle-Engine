'use client';

import React, { useRef, useState } from 'react';
import { HeroHeader } from '@/components/HeroHeader';
import { SiteHeader } from '@/components/SiteHeader';
import { ProductFeatures, WorkflowStrip, SiteFooter } from '@/components/ProductStory';
import { TurtleMark } from '@/components/BrandLogo';
import { PrInput } from '@/components/PrInput';
import { PhaseTimeline } from '@/components/PhaseTimeline';
import { CouncilChamber } from '@/components/CouncilChamber';
import { TrustAuditVisualizer } from '@/components/TrustAuditVisualizer';
import { FinalReportView } from '@/components/FinalReportView';
import { LanguageRejectionModal } from '@/components/LanguageRejectionModal';
import { useDemoReplay } from '@/hooks/useDemoReplay';
import { useReviewStream } from '@/hooks/useReviewStream';
import { Radio, RotateCcw, FastForward, Play, Pause, AlertCircle, KeyRound, ArrowUpRight } from 'lucide-react';

type ReviewMode = 'idle' | 'demo' | 'live';

export default function HomePage() {
  const [mode, setMode] = useState<ReviewMode>('idle');
  const [activePrUrl, setActivePrUrl] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [speed, setSpeed] = useState(1);
  const workspaceRef = useRef<HTMLElement>(null);
  const demo = useDemoReplay({ speedMultiplier: 1 });
  const live = useReviewStream({ accessToken });

  const handleStartDemo = () => {
    live.reset();
    demo.reset();
    setSpeed(1);
    demo.setSpeed(1);
    setMode('demo');
    demo.start();
    workspaceRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  };
  const handleStartLive = async (prUrl: string) => {
    demo.reset();
    live.reset();
    setActivePrUrl(prUrl);
    setMode('live');
    await live.startReview(prUrl);
  };
  const handleReset = () => {
    demo.reset();
    live.reset();
    setMode('idle');
    setActivePrUrl('');
  };

  const isDemo = mode === 'demo';
  const isReviewActive = mode !== 'idle';
  const isCompleted = isDemo ? demo.isCompleted : live.isCompleted;
  const verdict = isDemo ? demo.verdict : live.finalVerdict;
  const reviewError = isDemo ? demo.error : live.error;

  return (
    <>
      <a href="#workspace" className="skip-link">Skip to review workspace</a>
      <SiteHeader />
      <main>
        <HeroHeader onTryDemo={handleStartDemo} />
        <WorkflowStrip />
        <ProductFeatures />
        <section id="workspace" ref={workspaceRef} className={`workspace-section ${isReviewActive ? 'workspace-active' : ''}`} aria-labelledby="workspace-title">
          <div className="page-width">
            <div className="section-heading workspace-heading">
              <div>
                <span className="eyebrow">YOUR NEXT PULL REQUEST, WITH PERSPECTIVE.</span>
                <h2 id="workspace-title">Give your code a second look.</h2>
              </div>
              <span className="beta-label">
                <span className="status-dot" /> C# / .NET · Self-hosted beta</span>
            </div>
            {!isReviewActive ? (
              <div className="workspace-card">
                <div className="workspace-card-heading">
                  <span className="workspace-mark">
                    <TurtleMark />
                  </span>
                  <div>
                    <h3>Let’s take a look.</h3>
                    <p>Paste a GitHub pull request URL to start a council review.</p>
                  </div>
                  <span className="workspace-language">C#</span>
                </div>
                <PrInput onSubmit={handleStartLive} onTryDemo={handleStartDemo} showDemo={false} isLoading={live.isStreaming} />
                <details className="access-settings">
                  <summary>
                    <KeyRound size={14} /> Connect your local beta server <span>Optional for the sample review</span>
                  </summary>
                  <div className="access-settings-body">
                    <label htmlFor="access-key">Local beta access key</label>
                    <input id="access-key" type="password" autoComplete="off" aria-describedby="access-help" value={accessToken} onChange={event => setAccessToken(event.target.value)} placeholder="Enter your server access key" />
                    <p id="access-help">Use the key from your self-hosted server. It stays in this page’s memory. Live reviews require a configured backend and trusted repositories.</p>
                  </div>
                </details>
                <div className="workspace-help">
                  <span>Just exploring? The recorded sample runs without a server or an API key.</span>
                  <button onClick={handleStartDemo} className="text-link">Watch the sample <ArrowUpRight size={14} />
                  </button>
                </div>
              </div>
            ) : (
              <div className="review-console">
                <div className="review-control-bar">
                  <div className="review-mode">
                    <span className="status-dot" />{isDemo ? 'Sample review' : <>
                      <Radio size={14} /> Live council review</>}<span className="review-state">{isCompleted ? 'Complete' : 'In progress'}</span>
                  </div>
                  <div className="review-controls">
                    {isDemo && !isCompleted && <>
                      <button onClick={() => demo.isPlaying ? demo.pause() : demo.resume()}>{demo.isPlaying ? <Pause size={13} /> : <Play size={13} />}{demo.isPlaying ? 'Pause' : 'Resume'}</button>
                      <button onClick={demo.skipToEnd}>
                        <FastForward size={13} /> Skip to End</button>
                      <div className="speed-controls" aria-label="Replay speed">{[1, 2, 5].map(value => <button key={value} aria-pressed={speed === value} onClick={() => { setSpeed(value); demo.setSpeed(value); }}>{value}x</button>)}</div>
                    </>}
                    {!isDemo && live.isStreaming && <button onClick={live.cancel}>Cancel Review</button>}
                    <button onClick={handleReset}>
                      <RotateCcw size={13} />{isCompleted ? 'Review Another PR' : 'Reset'}</button>
                  </div>
                </div>
                <p className="review-disclosure">{isDemo ? 'Pre-recorded sample. No model calls are made. Historical citation evidence may be unavailable.' : activePrUrl}</p>
                <div className="review-content">
                  <PhaseTimeline currentPhase={isDemo ? demo.currentPhase : live.phase} phaseMessage={isDemo ? demo.phaseMessage : live.phaseMessage} />
                  {reviewError && !live.rejection?.isOpen && <div role="alert" className="review-error">
                    <AlertCircle size={18} />{typeof reviewError === 'string' ? reviewError : reviewError.message}</div>}
                  <CouncilChamber personaStates={isDemo ? demo.personaStates : live.personas} findings={isDemo ? demo.findings : live.findings} title="AI Review Council" />
                  <TrustAuditVisualizer guardAudit={isDemo ? demo.guardAudit : live.auditSymbols} />
                  {isCompleted && verdict && <FinalReportView verdict={verdict} markdown={isDemo ? demo.completedMarkdown : live.completedMarkdown} />}
                </div>
              </div>
            )}
            <p className="workspace-footnote">Compiler facts inform the review. Citation checks establish symbol-name existence; recommendations still need your judgment.</p>
          </div>
        </section>
      </main>
      <SiteFooter />
      <LanguageRejectionModal isOpen={Boolean(live.rejection?.isOpen)} type={live.rejection?.type} detectedLanguage={live.rejection?.detectedLanguage} customMessage={live.rejection?.customMessage} onClose={live.clearRejection} onRetry={handleStartDemo} />
    </>
  );
}
