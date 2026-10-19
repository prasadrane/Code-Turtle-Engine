import React, { useEffect, useState } from 'react';
import { GitPullRequest, ShieldCheck, FileCode2, Loader2, AlertCircle } from 'lucide-react';
import type { CouncilVerdict, GuardResult, ReviewFinding } from '@/lib/types';
import type { ReviewChanges, ReviewStatus, ReportOrigin } from '@/lib/changes';
import type { PersonaState } from '@/hooks/useDemoReplay';
import { TURTLE_CHARACTERS } from '@/lib/characters';
import { ReviewTabs } from './ReviewTabs';
import { FinalReportView } from './FinalReportView';
import { FindingCard } from './FindingCard';
import { CodeContextViewer } from './CodeContextViewer';
import { TrustAuditVisualizer, evidenceCounts } from './TrustAuditVisualizer';

export interface ReviewWorkspaceProps {
  status: ReviewStatus; isDemo: boolean; findings: ReviewFinding[]; verdict?: CouncilVerdict | null; audit: GuardResult[];
  personas: Record<string, PersonaState>; phase: string; message: string; markdown?: string | null; changes?: ReviewChanges | null;
  changesError?: string | null; stats?: Record<string, unknown> | null; error?: string | null; prUrl?: string;
  hasStarted?: boolean; reportOrigin?: ReportOrigin; controls?: React.ReactNode; onRetry?: () => void; onSample?: () => void;
}
export function ReviewWorkspace(props: ReviewWorkspaceProps) {
  const [active, setActive] = useState('overview');
  const [selectedFinding, setSelectedFinding] = useState<number | null>(null);
  const [returnFocus, setReturnFocus] = useState<number | null>(null);
  // Artifact anchor indices refer to the FINAL synthesized array, never streaming order.
  const viewerChanges = props.changes && props.status !== 'completed' ? { ...props.changes, anchors: [] } : props.changes;
  useEffect(() => {
    if (active !== 'overview' || returnFocus === null) return;
    document.querySelector<HTMLButtonElement>(`[data-inspect-finding="${returnFocus}"]`)?.focus();
    setReturnFocus(null);
  }, [active, returnFocus]);
  const snapshot = props.changes?.snapshot;
  const counts = evidenceCounts(props.audit);
  const statusText = props.status === 'failed' ? props.hasStarted ? 'Review interrupted' : 'Could not start review'
    : props.status === 'cancelled' ? 'Cancelled' : props.status === 'completed' ? 'Complete' : props.status === 'submitting' ? 'Submitting' : props.status === 'queued' ? 'Queued' : 'In progress';
  return <div className="review-workspace" data-testid="review-workspace">
    <header className="workspace-review-header"><div className="review-identity"><span className="review-pr-icon"><GitPullRequest size={21} /></span>
      <div><div className="review-repository">{snapshot ? `${snapshot.repository} / pull request #${snapshot.pullNumber}` : props.isDemo ? 'Code Turtle / sample review' : 'Your pull request'}</div>
        <h3>{snapshot?.title || 'A closer look at your changes'}</h3>{snapshot && <p className="revision-label">{props.reportOrigin === 'illustrative' ? 'Fixture head' : 'Analyzed head'} <code>{snapshot.headSha.slice(0, 8)}</code> · {snapshot.comparisonKind === 'head-only' ? 'Head source only' : 'PR merge-base comparison'}</p>}</div></div>
      <span className={`review-status review-status-${props.status}`} role="status"><span className="status-dot" />{statusText}</span></header>
    <div className="workspace-control-row"><p className="sample-disclosure">{props.isDemo ? props.reportOrigin === 'illustrative'
      ? 'Illustrative sample · authored findings and fixture source/diff. No model calls are made.'
      : 'Pre-recorded sample. No model calls are made. Historical citation evidence may be unavailable.' : props.prUrl}</p><div className="workspace-actions">{props.controls}</div></div>
    {props.status === 'failed' || props.status === 'cancelled' ? <div className="workspace-failure" role="alert"><AlertCircle size={20} /><div><strong>{statusText}</strong>
      <p>{props.error || 'The review was cancelled. Any provisional findings below have not completed citation checks.'}</p>
      <p>{!props.hasStarted ? 'Live reviews need a configured trusted-local server, an access key, and an allowed repository.' : 'The job may have stopped or its stream became unavailable. Partial findings remain provisional.'}</p>
      <div>{props.onRetry && <button type="button" onClick={props.onRetry}>Retry review</button>}{props.onSample && <button type="button" onClick={props.onSample}>Explore sample</button>}
        <a href="#connection-help">Server connection help</a></div></div></div> : props.status !== 'completed' && <div className="compact-progress" data-testid="phase-timeline" role="status"><Loader2 size={16} className="working-spinner" /><span>{props.message || 'Preparing your review…'}</span><small>{props.phase}</small></div>}
    <ReviewTabs tabs={[{ id: 'overview', label: 'Review overview' }, { id: 'changes', label: 'Changes' }, { id: 'evidence', label: 'Evidence' }]}
      active={active} onChange={next => { setActive(next); setSelectedFinding(null); }} label="Review workspace">
      {active === 'overview' && <>
        {props.status === 'completed' && props.verdict ? <FinalReportView verdict={props.verdict} markdown={props.markdown} stats={props.stats} reportOrigin={props.reportOrigin}
          anchoredFindings={props.changes?.anchors.map(anchor => anchor.findingIndex)} onInspect={index => { setSelectedFinding(index); setActive('changes'); }} />
          : <section className="provisional-review"><h2>Review notes</h2><p>Findings appear as reviewers finish. They remain provisional until arbitration and citation checks complete.</p>
            {props.findings.length ? props.findings.map((finding, index) => <FindingCard key={index} finding={finding} />) : <div className="workspace-empty"><FileCode2 size={24} /><p>Your review notes will appear here.</p></div>}</section>}
        {!props.changes && <p className="source-availability">Source and diff unavailable{props.changesError ? `: ${props.changesError}` : props.status === 'completed' ? ' for this review. Findings remain available above.' : ' until a source artifact is ready.'}</p>}
      </>}
      {active === 'changes' && (viewerChanges ? <><CodeContextViewer changes={viewerChanges} findings={props.findings} selectedFinding={selectedFinding}
        onReturn={selectedFinding === null ? undefined : () => { setReturnFocus(selectedFinding); setSelectedFinding(null); setActive('overview'); }} />
        {props.status !== 'completed' && <p className="context-notice">Inline comments become available after the final findings are ordered. Streamed notes remain provisional.</p>}</>
        : <div className="workspace-empty"><FileCode2 size={26} /><h2>Source and diff unavailable</h2><p>{props.changesError || 'This review has no source artifact to display. Your findings are in Review overview.'}</p><button onClick={() => setActive('overview')}>Back to findings</button></div>)}
      {active === 'evidence' && <TrustAuditVisualizer guardAudit={props.audit} />}
    </ReviewTabs>
    <div className="reviewer-strip" data-testid="council-chamber" aria-label="Review council">{['speedy', 'sheldon', 'sensei'].map((key, index) => {
      const character = TURTLE_CHARACTERS[key];
      const state = props.personas[character.role];
      return <details key={key}><summary><span className="reviewer-initial">{character.name.slice(0, 1)}</span><span><strong>{character.name}</strong><small>{['Performance', 'Security', 'Architecture'][index]}</small></span>
        <span className={`reviewer-state reviewer-${state?.status ?? 'idle'}`}>{state?.status ?? 'idle'}</span></summary><p>{character.bio}</p>{state?.quip && <p>{state.quip}</p>}</details>;
    })}<button type="button" className="evidence-shortcut" onClick={() => setActive('evidence')}><ShieldCheck size={19} /><span>{props.audit.length ? `${counts.verified} supported · ${counts.rejected} rejected · ${counts.unavailable} unavailable` : 'Citation evidence · not evaluated'}</span></button></div>
  </div>;
}
