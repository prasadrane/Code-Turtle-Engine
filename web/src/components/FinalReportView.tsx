import React, { useState, useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  FileText,
  Users,
  CheckCheck,
  Copy,
  Check,
  AlertTriangle,
  ShieldCheck,
  Terminal,
  Search,
  Filter,
} from 'lucide-react';
import type { CouncilVerdict } from '../lib/types';
import { FindingCard } from './FindingCard';
import { PersonaBreakdownPanel } from './PersonaBreakdownPanel';

export interface FinalReportViewProps {
  verdict?: CouncilVerdict | null;
  markdown?: string | null;
  stats?: Record<string, unknown> | null;
  className?: string;
}

type TabType = 'synthesized' | 'personas' | 'markdown';
export function FinalReportView({
  verdict,
  markdown,
  stats,
  className = '',
}: FinalReportViewProps) {
  const [activeTab, setActiveTab] = useState<TabType>('synthesized');
  const [copied, setCopied] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedPersona, setSelectedPersona] = useState<string>('all');
  const [selectedSeverity, setSelectedSeverity] = useState<string>('all');

  const synthesizedFindings = verdict?.synthesized ?? [];
  const personasVerdicts = verdict?.personas ?? [];
  const guardAudit = verdict?.guardAudit ?? [];
  const rawMarkdown = markdown || '';

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(rawMarkdown);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback
    }
  };

  const filteredFindings = useMemo(() => {
    return synthesizedFindings.filter((f) => {
      if (selectedPersona !== 'all' && f.persona !== selectedPersona) return false;
      if (selectedSeverity !== 'all' && f.severity?.toLowerCase() !== selectedSeverity.toLowerCase()) {
        return false;
      }
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchTitle = f.title.toLowerCase().includes(q);
        const matchDetail = f.detail.toLowerCase().includes(q);
        const matchLoc = f.location?.toLowerCase().includes(q);
        const matchSym = f.citedSymbolFqns?.some((s) => s.toLowerCase().includes(q));
        if (!matchTitle && !matchDetail && !matchLoc && !matchSym) return false;
      }
      return true;
    });
  }, [synthesizedFindings, selectedPersona, selectedSeverity, searchQuery]);

  const hasCriticalOrError = synthesizedFindings.some((f) => {
    const s = (f.severity || '').toLowerCase();
    return s === 'critical' || s === 'error';
  });

  return (
    <div
      data-testid="final-report-view"
      className={`w-full max-w-7xl mx-auto rounded-2xl bg-slate-900/90 border border-slate-800/80 p-6 shadow-2xl backdrop-blur-md flex flex-col space-y-6 ${className}`}
    >
      {/* Header with Title and Verdict Badge */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-5 border-b border-slate-800">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
            <FileText className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-slate-100 tracking-tight">
              Code Turtle · Final review
            </h2>
            <p className="text-xs text-slate-400">
              Arbitrated findings with citation existence checks. Recommendations require review.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {hasCriticalOrError ? (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-rose-500/15 text-rose-300 border border-rose-500/30 shadow-md shadow-rose-500/10">
              <AlertTriangle className="w-4 h-4 text-rose-400" />
              Changes Required ({synthesizedFindings.length} findings)
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 shadow-md shadow-emerald-500/10">
              <ShieldCheck className="w-4 h-4 text-emerald-400" />
              Review completed ({synthesizedFindings.length} findings)
            </span>
          )}
        </div>
      </div>

      <p className="text-xs text-amber-300">
        {(typeof stats?.personasSucceeded === 'number' ? stats.personasSucceeded : personasVerdicts.length) < 3 && 'Degraded review: fewer than three personas succeeded. '}
        {synthesizedFindings.length === 0 && personasVerdicts.some(p => p.findings.length > 0) && 'All raw findings were removed by arbitration or citation policy. '}
        {guardAudit.some(a => !a.evidence || a.evidence === 'LegacyUnknown') && 'Legacy demo: citation evidence provenance is unavailable. '}
      </p>
      {/* Tabs and Controls */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
        <div role="tablist" className="flex items-center gap-2 p-1 rounded-xl bg-slate-950/60 border border-slate-800 self-start">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'synthesized'}
            onClick={() => setActiveTab('synthesized')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
              activeTab === 'synthesized'
                ? 'bg-slate-800 text-sky-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <CheckCheck className="w-4 h-4" />
            <span>Synthesized Verdict</span>
            <span className="ml-1 px-1.5 py-0.5 rounded-md text-[10px] bg-slate-700 text-slate-300">
              {synthesizedFindings.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'personas'}
            onClick={() => setActiveTab('personas')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
              activeTab === 'personas'
                ? 'bg-slate-800 text-sky-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <Users className="w-4 h-4" />
            <span>Persona Breakdown</span>
            <span className="ml-1 px-1.5 py-0.5 rounded-md text-[10px] bg-slate-700 text-slate-300">
              {personasVerdicts.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'markdown'}
            onClick={() => setActiveTab('markdown')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
              activeTab === 'markdown'
                ? 'bg-slate-800 text-sky-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <Terminal className="w-4 h-4" />
            <span>Markdown Review</span>
          </button>
        </div>

        {activeTab === 'synthesized' && synthesizedFindings.length > 0 && (
          <div className="relative">
            <Search className="w-3.5 h-3.5 text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              data-testid="finding-search-input"
              placeholder="Filter by keyword, line, or symbol..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-8 pr-3 py-1.5 text-xs bg-slate-950/80 border border-slate-800 rounded-lg text-slate-200 placeholder-slate-500 focus:outline-none focus:border-sky-500 w-64 transition-all"
            />
          </div>
        )}
      </div>

      {activeTab === 'synthesized' && synthesizedFindings.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 pt-1 border-t border-slate-800/60">
          <span className="text-[11px] font-semibold text-slate-400 flex items-center gap-1">
            <Filter className="w-3 h-3" /> Filters:
          </span>
          {['all', 'Critical', 'Error', 'Warning'].map((sev) => (
            <button
              key={sev}
              type="button"
              data-testid={`filter-severity-${sev.toLowerCase()}`}
              onClick={() => setSelectedSeverity(sev)}
              className={`px-2.5 py-1 rounded-md text-[11px] font-medium border transition-all ${
                selectedSeverity.toLowerCase() === sev.toLowerCase()
                  ? 'bg-sky-500/20 text-sky-300 border-sky-500/40 shadow-sm'
                  : 'bg-slate-950/40 text-slate-400 border-slate-800 hover:text-slate-200'
              }`}
            >
              {sev === 'all' ? 'All Severities' : sev}
            </button>
          ))}
          <div className="h-3 w-px bg-slate-800 mx-1" />
          {[
            { id: 'all', label: 'All Personas' },
            { id: 'AllocationsPerformance', label: '⚡ Speedy (Perf)' },
            { id: 'SecurityAuditor', label: '🔒 Sheldon (Sec)' },
            { id: 'IdiomaticArchitect', label: '🏯 Sensei (Arch)' },
          ].map((p) => (
            <button
              key={p.id}
              type="button"
              data-testid={`filter-persona-${p.id}`}
              onClick={() => setSelectedPersona(p.id)}
              className={`px-2.5 py-1 rounded-md text-[11px] font-medium border transition-all ${
                selectedPersona === p.id
                  ? 'bg-sky-500/20 text-sky-300 border-sky-500/40 shadow-sm'
                  : 'bg-slate-950/40 text-slate-400 border-slate-800 hover:text-slate-200'
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>
      )}

      {/* Tab Panels */}
      <div className="w-full">
        {activeTab === 'synthesized' && (
          <motion.div
            key="synthesized"
            role="tabpanel"
            data-testid="synthesized-verdict-panel"
            initial={{ opacity: 0, y: 6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col space-y-4"
          >
            {filteredFindings.length === 0 ? (
              <div className="p-8 text-center rounded-xl bg-slate-950/40 border border-slate-800 text-slate-400 text-sm">
                {synthesizedFindings.length === 0
                  ? 'No retained findings. This does not establish that the code is clean.'
                  : 'No findings match the current filter selection.'}
              </div>
            ) : (
              filteredFindings.map((finding, idx) => (
                <FindingCard
                  key={`${finding.title}-${idx}`}
                  finding={finding}
                  guardAudit={guardAudit}
                />
              ))
            )}
          </motion.div>
        )}

        {activeTab === 'personas' && (
          <PersonaBreakdownPanel personasVerdicts={personasVerdicts} guardAudit={guardAudit} retained={synthesizedFindings} />
        )}

        {activeTab === 'markdown' && (
          <motion.div
            key="markdown"
            role="tabpanel"
            data-testid="markdown-review-panel"
            initial={{ opacity: 0, y: 6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col space-y-3"
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
                Raw Markdown Output
              </span>
              <button
                type="button"
                onClick={handleCopy}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition-colors"
                aria-label="Copy Markdown"
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{copied ? 'Copied!' : 'Copy Markdown'}</span>
              </button>
            </div>
            <pre className="p-4 rounded-xl bg-slate-950 border border-slate-800 text-xs font-mono text-slate-300 overflow-x-auto whitespace-pre-wrap leading-relaxed">
              {rawMarkdown || 'No markdown report available.'}
            </pre>
          </motion.div>
        )}
      </div>
    </div>
  );
}
