import React, { useLayoutEffect, useRef } from 'react';
import { ArrowUpRight } from 'lucide-react';
import type { FindingAnchor } from '@/lib/changes';
import type { GuardResult, ReviewFinding } from '@/lib/types';
import { FindingCard } from './FindingCard';

export function FindingsInspector({ findings, anchors, audit, selectedFinding, onInspect, active, open, id, completed }: {
  findings: ReviewFinding[]; anchors: FindingAnchor[]; audit: GuardResult[]; selectedFinding: number | null;
  onInspect: (index: number, origin: HTMLButtonElement) => void; active: boolean; open: boolean; id: string; completed: boolean;
}) {
  const pane = useRef<HTMLElement>(null);
  const scrollTop = useRef(0);
  useLayoutEffect(() => {
    if (active && open && pane.current) pane.current.scrollTop = scrollTop.current;
  }, [active, open]);
  return <aside id={id} ref={pane} className="findings-inspector" aria-label="Findings inspector" hidden={!open}
    onScroll={event => { scrollTop.current = event.currentTarget.scrollTop; }}>
    <div className="inspector-heading"><h2>Retained findings</h2><span className="quiet-badge">{findings.length}</span></div>
    <p className="inspector-description">All retained findings. Code anchors provide context; they do not verify recommendations.</p>
    <div className="inspector-findings">{findings.map((finding, index) => {
      const anchor = anchors.find(item => item.findingIndex === index);
      return <div key={`${index}-${finding.title}`} className={`inspector-finding${selectedFinding === index ? ' inspector-selected' : ''}`}>
        <FindingCard finding={finding} guardAudit={audit} />
        {anchor ? <button type="button" className="inspect-finding" aria-label={`View code context: ${finding.title}`}
          aria-pressed={selectedFinding === index} onClick={event => onInspect(index, event.currentTarget)}>
          Code anchor <ArrowUpRight size={14} /><span className="inspector-anchor-path">{anchor.path}:{anchor.startLine}</span>
        </button> : <p className="inspector-no-anchor">No code anchor · recommendation remains available here.</p>}
      </div>;
    })}</div>
    {!findings.length && <p className="workspace-empty">{completed ? 'No retained findings. This does not establish that the code is clean.' : 'Retained findings become available after the review completes.'}</p>}
  </aside>;
}
