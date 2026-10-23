import React, { useEffect, useId, useState } from 'react';
import { ChevronDown } from 'lucide-react';
import type { PersonaState } from '@/hooks/useDemoReplay';
import type { ReviewStatus } from '@/lib/changes';
import { TURTLE_CHARACTERS } from '@/lib/characters';

export const CITATION_LIMITS = 'Compiler facts inform the review. Citation checks establish symbol-name existence; recommendations still need your judgment.';

export function StatusBar({ status, phase, message, personas, replayControls }: {
  status: ReviewStatus; phase: string; message: string; personas: Record<string, PersonaState>;
  replayControls?: React.ReactNode;
}) {
  const [expanded, setExpanded] = useState(true);
  const contentId = useId();
  useEffect(() => {
    if (!window.matchMedia) return;
    const query = window.matchMedia('(min-width: 768px)');
    const update = () => setExpanded(query.matches);
    update();
    query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  }, []);
  const terminal = status === 'completed' ? 'Review complete.' : status === 'cancelled' ? 'Review cancelled.'
    : status === 'failed' ? 'Review stopped. Check the report for details.' : null;
  const phaseLabel = status === 'completed' ? 'Complete' : status === 'cancelled' ? 'Cancelled'
    : status === 'failed' ? 'Interrupted' : phase.replace(/[_-]/g, ' ') || 'Preparing';
  const completed = Object.values(personas).filter(persona => persona.status === 'completed' || persona.status === 'failed').length;
  return <footer className="app-status-bar" aria-label="Review status and playback">
    <div className="status-phase" role="status" data-testid="phase-timeline">
      <strong>{phaseLabel}</strong><span>{terminal || message || 'Preparing your review…'}</span>
    </div>
    <button type="button" className="status-toggle" aria-expanded={expanded} aria-controls={contentId}
      onClick={() => setExpanded(value => !value)}>Reviewers {completed}/3{replayControls ? ' · Playback' : ''}<ChevronDown size={15} /></button>
    <div className="app-status-content" id={contentId} data-expanded={expanded}>
      <div className="status-reviewers" data-testid="council-chamber" aria-label="Review council">
        {['speedy', 'sheldon', 'sensei'].map((key, index) => {
          const character = TURTLE_CHARACTERS[key];
          const state = personas[character.role];
          return <details key={key}>
            <summary><span className="reviewer-initial">{character.name.slice(0, 1)}</span>
              <strong>{character.name}</strong><span className={`reviewer-state reviewer-${state?.status ?? 'idle'}`}>{state?.status ?? 'idle'}</span></summary>
            <div><strong>{['Performance', 'Security', 'Architecture'][index]}</strong>
              <p>{character.bio}</p><p>{state?.findingCount ?? 0} finding(s)</p>{state?.quip && <p>{state.quip}</p>}</div>
          </details>;
        })}
      </div>
      {replayControls && <div className="status-replay workspace-actions" aria-label="Sample playback">{replayControls}</div>}
    </div>
    <p className="workspace-footnote status-limitations">{CITATION_LIMITS}</p>
  </footer>;
}
