import React, { useMemo, useState } from 'react';
import { Copy, Check, Search, ArrowUpRight } from 'lucide-react';
import type { CouncilVerdict } from '@/lib/types';
import { reportWithProvenance, type ReportOrigin } from '@/lib/changes';
import { FindingCard } from './FindingCard';
import { PersonaBreakdownPanel } from './PersonaBreakdownPanel';
import { ReviewTabs } from './ReviewTabs';

export interface FinalReportViewProps {
  verdict?: CouncilVerdict | null; markdown?: string | null; stats?: Record<string, unknown> | null;
  className?: string; reportOrigin?: ReportOrigin; onInspect?: (findingIndex: number) => void; anchoredFindings?: number[];
}
const tabs = [{ id: 'synthesized', label: 'Synthesized Verdict' }, { id: 'personas', label: 'Persona Breakdown' }, { id: 'markdown', label: 'Markdown Review' }];

export function FinalReportView({ verdict, markdown, stats, className = '', reportOrigin = 'live', onInspect, anchoredFindings = [] }: FinalReportViewProps) {
  const [activeTab, setActiveTab] = useState('synthesized');
  const [copied, setCopied] = useState(false);
  const [copyError, setCopyError] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedPersona, setSelectedPersona] = useState('all');
  const [selectedSeverity, setSelectedSeverity] = useState('all');
  const findings = verdict?.synthesized ?? [];
  const personas = verdict?.personas ?? [];
  const audit = verdict?.guardAudit ?? [];
  const report = reportWithProvenance(markdown ?? '', reportOrigin);
  const filtered = useMemo(() => findings.map((finding, index) => ({ finding, index })).filter(({ finding }) => {
    if (selectedPersona !== 'all' && finding.persona !== selectedPersona) return false;
    if (selectedSeverity !== 'all' && finding.severity.toLowerCase() !== selectedSeverity.toLowerCase()) return false;
    const query = searchQuery.trim().toLowerCase();
    return !query || [finding.title, finding.detail, finding.location, ...finding.citedSymbolFqns].some(value => value?.toLowerCase().includes(query));
  }), [findings, selectedPersona, selectedSeverity, searchQuery]);
  const priorities = findings.filter(finding => ['Critical', 'Error'].includes(finding.severity)).length;
  const succeeded = typeof stats?.personasSucceeded === 'number' ? stats.personasSucceeded : personas.length;
  return <section data-testid="final-report-view" className={`final-report ${className}`} aria-label="Final review">
    <div className="report-heading"><div><span className="eyebrow">A CLOSER LOOK AT YOUR CHANGES</span><h2>Code Turtle · Final review</h2>
      <p>Retained recommendations, ready for your judgment.</p></div>
      <span className={`quiet-badge ${priorities ? 'priority-badge' : ''}`}>{priorities ? `${priorities} high-priority ${priorities === 1 ? 'finding' : 'findings'}` : `${findings.length} retained findings`}</span></div>
    <p className="report-caveat">{succeeded < 3 && 'Degraded review: fewer than 3 reviewers succeeded. '}
      {findings.length === 0 && personas.some(persona => persona.findings.length > 0) && 'All raw findings were removed after arbitration / guard. '}
      Citation checks establish symbol-name existence. Recommendations and model locations are not verified.</p>
    <ReviewTabs tabs={tabs} active={activeTab} onChange={setActiveTab} label="Review report">
      {activeTab === 'synthesized' && <div data-testid="synthesized-verdict-panel">
        {findings.length > 0 && <div className="finding-filters">
          <label className="finding-search"><Search size={16} /><span className="sr-only">Search findings</span>
            <input aria-label="Search findings" data-testid="finding-search-input" placeholder="Search findings, paths, or symbols" value={searchQuery} onChange={event => setSearchQuery(event.target.value)} /></label>
          <div className="filter-group" aria-label="Severity filter">{['all', 'Critical', 'Error', 'Warning', 'Nit', 'Info'].map(severity => <button key={severity} type="button"
            data-testid={`filter-severity-${severity.toLowerCase()}`} aria-pressed={selectedSeverity === severity} onClick={() => setSelectedSeverity(severity)}>{severity === 'all' ? 'All Severities' : severity}</button>)}</div>
          <div className="filter-group" aria-label="Reviewer filter">{[{ id: 'all', label: 'All Personas' }, { id: 'AllocationsPerformance', label: 'Speedy (Perf)' },
            { id: 'SecurityAuditor', label: 'Sheldon (Sec)' }, { id: 'IdiomaticArchitect', label: 'Sensei (Arch)' }].map(persona => <button key={persona.id} type="button"
              data-testid={`filter-persona-${persona.id}`} aria-pressed={selectedPersona === persona.id} onClick={() => setSelectedPersona(persona.id)}>{persona.label}</button>)}</div>
          <p role="status" className="filter-count">{filtered.length} of {findings.length} retained findings</p>
        </div>}
        <div className="finding-list">{filtered.length ? filtered.map(({ finding, index }) => <div key={`${finding.title}-${index}`}>
          <FindingCard finding={finding} guardAudit={audit} />
          {onInspect && anchoredFindings.includes(index) && <button type="button" className="inspect-finding" data-inspect-finding={index} onClick={() => onInspect(index)}>View code context <ArrowUpRight size={14} /></button>}
        </div>) : <p className="workspace-empty">{findings.length ? 'No findings match the current filter selection.' : 'No retained findings. This does not establish that the code is clean.'}</p>}</div>
      </div>}
      {activeTab === 'personas' && <PersonaBreakdownPanel personasVerdicts={personas} guardAudit={audit} retained={findings} />}
      {activeTab === 'markdown' && <div data-testid="markdown-review-panel" className="markdown-report">
        <div className="markdown-toolbar"><div><strong>{reportOrigin === 'historical' ? 'Historical report output' : 'Review report'}</strong>
          <p>Provenance and limitations are included when you copy.</p></div><button type="button" disabled={!markdown} onClick={async () => {
          try { await navigator.clipboard.writeText(report); setCopied(true); setCopyError(''); }
          catch { setCopyError('Could not copy. Select the report text to copy it manually.'); }
        }}>{copied ? <Check size={15} /> : <Copy size={15} />}{copied ? 'Copied!' : 'Copy Markdown'}</button></div>
        {copyError && <p role="status">{copyError}</p>}<pre>{markdown ? report : 'No markdown report available.'}</pre>
      </div>}
    </ReviewTabs>
  </section>;
}
