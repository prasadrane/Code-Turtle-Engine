import React from 'react';
import { RotateCcw, Square } from 'lucide-react';
import { BrandLogo } from './BrandLogo';
import type { ReviewChanges, ReviewStatus } from '@/lib/changes';

export function reviewStatusLabel(status: ReviewStatus, hasStarted = false) {
  return status === 'failed' ? hasStarted ? 'Review interrupted' : 'Could not start review'
    : status === 'cancelled' ? 'Cancelled' : status === 'completed' ? 'Complete'
      : status === 'submitting' ? 'Submitting' : status === 'queued' ? 'Queued'
        : status === 'idle' ? 'Ready' : 'In progress';
}

function prIdentity(url?: string) {
  if (!url) return null;
  try {
    const match = new URL(url).pathname.match(/^\/([^/]+)\/([^/]+)\/pull\/(\d+)(?:\/|$)/);
    return match ? { repository: `${match[1]}/${match[2]}`, pullNumber: match[3] } : null;
  } catch { return null; }
}

export function AppBar({ active, isDemo, status, hasStarted, snapshot, prUrl, isStreaming, onCancel, onNewReview }: {
  active: boolean; isDemo: boolean; status: ReviewStatus; hasStarted?: boolean;
  snapshot?: ReviewChanges['snapshot']; prUrl?: string; isStreaming: boolean;
  onCancel: () => void; onNewReview: () => void;
}) {
  const identity = snapshot ?? prIdentity(prUrl);
  return <header className="app-bar">
    <a className="app-bar-brand" href="/" aria-label="Code Turtle home"><BrandLogo /></a>
    <div className="app-bar-identity">
      {active ? <>
        <div className="app-bar-context">{identity ? <><code>{identity.repository}</code><span> · PR #{identity.pullNumber}</span></>
          : isDemo ? 'Code Turtle sample' : 'Your pull request'}</div>
        <h1>{snapshot?.title || (isDemo ? 'Illustrative review' : 'Live review')}</h1>
        {snapshot && <small className="revision-label">{isDemo ? 'Fixture head' : 'Analyzed head'} <code>{snapshot.headSha.slice(0, 8)}</code>
          {' · '}{snapshot.comparisonKind === 'head-only' ? 'Head source only' : 'PR merge-base comparison'}</small>}
      </> : <span>Review workspace</span>}
    </div>
    {active && <><span className={`app-mode-pill app-mode-${isDemo ? 'sample' : 'live'}`}>{isDemo ? 'Sample' : 'Live'}</span>
      <span className={`app-bar-status review-status review-status-${status}`} role="status">
        <span className="status-dot" />{reviewStatusLabel(status, hasStarted)}</span></>}
    <div className="app-bar-actions workspace-actions">
      {active && !isDemo && isStreaming && <button type="button" onClick={onCancel}><Square size={13} />Cancel review</button>}
      <button type="button" onClick={onNewReview}><RotateCcw size={14} />New review</button>
    </div>
  </header>;
}
