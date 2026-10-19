import React from 'react';
import type { FindingAnchor } from '@/lib/changes';
import type { ReviewFinding } from '@/lib/types';
import { getCharacterByPersona } from '@/lib/characters';

export function InlineReviewComment({ finding, anchor, selected }: {
  finding: ReviewFinding; anchor: FindingAnchor; selected?: boolean;
}) {
  const character = getCharacterByPersona(finding.persona);
  return <article id={`inline-finding-${anchor.findingIndex}`} tabIndex={-1}
    data-testid={`inline-comment-${anchor.findingIndex}`} data-line={anchor.startLine}
    className={`inline-review-comment ${selected ? 'comment-selected' : ''}`}>
    <div className="inline-comment-meta"><span className={`severity severity-${finding.severity.toLowerCase()}`}>{finding.severity}</span>
      <span>{character?.name ?? finding.persona} · {anchor.provenance === 'FixtureLine' ? 'Illustrative fixture line' : 'Member context'}</span></div>
    <h3>{finding.title}</h3><p>{finding.detail}</p>
    <small>{anchor.provenance === 'FixtureLine' ? 'Authored sample comment; no model inference.' : `Roslyn member range ${anchor.startLine}–${anchor.endLine}. The defect location and recommendation are not verified.`}</small>
  </article>;
}
