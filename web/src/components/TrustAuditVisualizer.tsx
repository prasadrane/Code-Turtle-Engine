import React from 'react';
import type { GuardResult } from '@/lib/types';
import { TURTLE_CHARACTERS, type TurtleCharacter } from '@/lib/characters';

export interface TrustAuditVisualizerProps { guardAudit?: GuardResult[]; character?: TurtleCharacter; className?: string; title?: string; }
export function evidenceCounts(audit: GuardResult[]) {
  const verified = audit.filter(item => item.verified && (item.evidence === 'Payload' || item.evidence === 'Compilation')).length;
  const rejected = audit.filter(item => !item.verified).length;
  return { verified, rejected, unavailable: audit.length - verified - rejected };
}
export function TrustAuditVisualizer({ guardAudit = [], character = TURTLE_CHARACTERS.judge, className = '', title = 'Citation evidence' }: TrustAuditVisualizerProps) {
  const { verified, rejected, unavailable } = evidenceCounts(guardAudit);
  const rate = guardAudit.length > 0 && unavailable === 0 ? `${Math.round(verified / guardAudit.length * 100)}%` : null;
  return <section data-testid="trust-audit-visualizer" className={`evidence-panel ${className}`} aria-label={title}>
    <div className="report-heading"><div><span className="eyebrow">FOLLOW THE EVIDENCE</span><h2>{title}</h2><p>{character.name} · Citation existence checks</p></div>
      <div data-testid="trust-score-badge" className="evidence-counts"><strong>{verified} Verified</strong><span>{rejected} Rejected; {unavailable} evidence unavailable</span>
        <small>{rate ? `${rate} citation existence rate` : guardAudit.length ? 'Partial evidence · no combined rate' : 'Not evaluated'}</small></div></div>
    <details className="evidence-explainer" data-testid="roslyn-explainer"><summary>What do Roslyn compiler citation checks establish?</summary>
      <p>Payload-supported symbols appear in the review’s extracted compiler-fact payload. Prompt limits may reduce what is sent to the model. Compilation-only symbols exist in the broader compilation. These checks do not verify overloads, source locations, relevance, or recommendation correctness. Strip mode removes rejected citations; Flag mode retains them for inspection. Retention depends on finding policy.</p></details>
    <div className="evidence-table-scroll" tabIndex={0} role="region" aria-label="Citation evidence table"><table className="evidence-table"><caption className="sr-only">Symbol Verification Table ({guardAudit.length})</caption>
      <thead><tr><th scope="col">Cited symbol</th><th scope="col">Evidence</th><th scope="col">Meaning</th></tr></thead><tbody>
        {guardAudit.length === 0 ? <tr><td colSpan={3}>No cited symbols evaluated yet.</td></tr> : guardAudit.map((item, index) => {
          const supported = item.verified && (item.evidence === 'Payload' || item.evidence === 'Compilation');
          return <tr key={`${item.citedFqn}-${index}`}><td><code className={!item.verified ? 'citation-rejected' : ''}>{item.citedFqn}</code></td>
            <td><span data-testid={supported ? 'status-verified' : !item.verified ? 'status-stripped' : 'status-unavailable'} className={`evidence-chip ${supported ? 'evidence-supported' : !item.verified ? 'evidence-rejected' : ''}`}>
              {supported ? `Verified (${item.evidence})` : item.verified ? 'Legacy / evidence unavailable' : 'Citation Rejected'}</span></td>
            <td>{supported ? 'Symbol-name existence only' : item.verified ? 'No current verification claim' : 'Strip removes; Flag retains'}</td></tr>;
        })}
      </tbody></table></div>
  </section>;
}
