'use client';

import React, { useState } from 'react';
import { HeroHeader } from '@/components/HeroHeader';
import { PrInput } from '@/components/PrInput';
import { PhaseTimeline } from '@/components/PhaseTimeline';
import { CouncilChamber } from '@/components/CouncilChamber';
import { TrustAuditVisualizer } from '@/components/TrustAuditVisualizer';
import { FinalReportView } from '@/components/FinalReportView';
import { LanguageRejectionModal } from '@/components/LanguageRejectionModal';
import { useDemoReplay } from '@/hooks/useDemoReplay';
import { useReviewStream } from '@/hooks/useReviewStream';
import { Sparkles, Radio, RotateCcw, FastForward, Play, Pause, AlertCircle } from 'lucide-react';

type ReviewMode = 'idle' | 'demo' | 'live';

export default function HomePage() {
  const [mode, setMode] = useState<ReviewMode>('idle');
  const [activePrUrl, setActivePrUrl] = useState<string>('');

  const demo = useDemoReplay({ speedMultiplier: 1.5 });
  const live = useReviewStream();

  const handleStartDemo = () => {
    live.reset();
    demo.reset();
    setMode('demo');
    demo.start();
  };

  const handleStartLive = async (prUrl: string) => {
    demo.reset();
    live.reset();
    setActivePrUrl(prUrl);
    setMode('live');
    await live.startReview(prUrl);
  };

  const handleResetToIdle = () => {
    demo.reset();
    live.reset();
    setMode('idle');
    setActivePrUrl('');
  };

  // Determine active state values based on current mode
  const isDemo = mode === 'demo';
  const isLive = mode === 'live';
  const isReviewActive = isDemo || isLive;

  const currentPhase = isDemo ? demo.currentPhase : live.phase;
  const phaseMessage = isDemo ? demo.phaseMessage : live.phaseMessage;
  const personaStates = isDemo ? demo.personaStates : live.personas;
  const findings = isDemo ? demo.findings : live.findings;
  const guardAudit = isDemo ? demo.guardAudit : live.auditSymbols;
  const verdict = isDemo ? demo.verdict : live.finalVerdict;
  const markdown = isDemo ? demo.completedMarkdown : live.completedMarkdown;
  const isCompleted = isDemo ? demo.isCompleted : live.isCompleted;

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 selection:bg-emerald-500/30 flex flex-col items-center px-4 sm:px-6 lg:px-8 py-8 space-y-10">
      {/* Top Header */}
      <HeroHeader />

      {/* PR Input & Quick Select (Visible in idle or can be toggled) */}
      {mode === 'idle' && (
        <section className="w-full max-w-4xl mx-auto flex flex-col items-center space-y-6 pt-2">
          <PrInput
            onSubmit={handleStartLive}
            onTryDemo={handleStartDemo}
            isLoading={live.isStreaming}
          />
        </section>
      )}

      {/* Active Mode Control Bar */}
      {isReviewActive && (
        <section className="w-full max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4 p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-xl">
          <div className="flex items-center gap-3">
            {isDemo ? (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-xs font-semibold">
                <Sparkles className="w-3.5 h-3.5" />
                Demo Replay Mode
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-400 text-xs font-semibold">
                <Radio className="w-3.5 h-3.5 animate-pulse" />
                Live Council Review: <span className="font-mono text-[11px] truncate max-w-xs">{activePrUrl}</span>
              </span>
            )}
            {isCompleted && (
              <span className="px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 text-xs font-medium">
                Complete
              </span>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {isDemo && !isCompleted && (
              <>
                <button
                  type="button"
                  onClick={() => (demo.isPlaying ? demo.pause() : demo.resume())}
                  className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-200 border border-slate-700"
                >
                  {demo.isPlaying ? <Pause className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
                  <span>{demo.isPlaying ? 'Pause' : 'Resume'}</span>
                </button>
                <button
                  type="button"
                  onClick={demo.skipToEnd}
                  className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-200 border border-slate-700"
                >
                  <FastForward className="w-3.5 h-3.5" />
                  <span>Skip to End</span>
                </button>
                <div className="flex items-center gap-1 pl-1 text-[11px] font-mono text-slate-400">
                  <span>Speed:</span>
                  {[1, 2, 5].map((spd) => (
                    <button
                      key={spd}
                      type="button"
                      onClick={() => demo.setSpeed(spd)}
                      className="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white"
                    >
                      {spd}x
                    </button>
                  ))}
                </div>
              </>
            )}

            {isLive && live.isStreaming && (
              <button
                type="button"
                onClick={live.cancel}
                className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 border border-rose-500/30 text-rose-300 text-xs"
              >
                Cancel Review
              </button>
            )}

            <button
              type="button"
              onClick={handleResetToIdle}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-xs font-medium"
            >
              <RotateCcw className="w-3.5 h-3.5" />
              <span>{isCompleted ? 'Review Another PR' : 'Reset'}</span>
            </button>
          </div>
        </section>
      )}

      {/* Review Workflow Sections */}
      {isReviewActive && (
        <div className="w-full max-w-7xl mx-auto space-y-8 animate-fadeIn">
          {/* Timeline */}
          <PhaseTimeline currentPhase={currentPhase} phaseMessage={phaseMessage} />

          {/* Live Error Banner if any */}
          {live.error && !live.rejection?.isOpen && (
            <div className="flex items-center gap-2 p-4 rounded-xl bg-rose-950/60 border border-rose-800/80 text-rose-300 text-sm">
              <AlertCircle className="w-5 h-5 text-rose-400 flex-shrink-0" />
              <span>{typeof live.error === 'string' ? live.error : live.error.message}</span>
            </div>
          )}

          {/* Council Chamber */}
          <CouncilChamber personaStates={personaStates} findings={findings} />

          {/* Roslyn Trust Audit Visualizer */}
          <TrustAuditVisualizer guardAudit={guardAudit} />

          {/* Final Synthesized Report View */}
          {isCompleted && verdict && (
            <FinalReportView verdict={verdict} markdown={markdown} />
          )}
        </div>
      )}

      {/* Language Rejection Modal */}
      <LanguageRejectionModal
        isOpen={Boolean(live.rejection?.isOpen)}
        type={live.rejection?.type}
        detectedLanguage={live.rejection?.detectedLanguage}
        customMessage={live.rejection?.customMessage}
        onClose={live.clearRejection}
        onRetry={handleStartDemo}
      />
    </main>
  );
}
