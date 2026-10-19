import React from 'react';
import type { PersonaVerdict, GuardResult, ReviewFinding } from '@/lib/types';
import { getCharacterByPersona } from '@/lib/characters';
import { FindingCard } from './FindingCard';

export interface PersonaBreakdownPanelProps { personasVerdicts: PersonaVerdict[]; guardAudit?: GuardResult[]; retained?: ReviewFinding[]; }
export function PersonaBreakdownPanel({ personasVerdicts, guardAudit = [], retained = [] }: PersonaBreakdownPanelProps) {
  return <div data-testid="persona-breakdown-panel" className="persona-audit-list">
    <p className="report-caveat">Original reviewer output before arbitration and citation filtering. These are raw audit records.</p>
    {personasVerdicts.map(persona => <details key={persona.persona} open><summary><span>{getCharacterByPersona(persona.persona)?.name ?? persona.persona}</span> · {persona.findings.length} raw findings</summary>
      {persona.findings.length === 0 ? <p>No findings from this persona.</p> : persona.findings.map((finding, index) => <div key={index} className="raw-finding">
        <p className="report-caveat">Raw persona audit view — {retained.some(item => item.title === finding.title && item.location === finding.location && item.persona === finding.persona) ? 'retained; citations may be stripped' : 'not retained after arbitration / guard'}</p>
        <FindingCard finding={finding} guardAudit={guardAudit} /></div>)}
    </details>)}
  </div>;
}
