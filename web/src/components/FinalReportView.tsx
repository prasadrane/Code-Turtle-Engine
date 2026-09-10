import React, { useState } from 'react';
import { motion } from 'framer-motion';
import {
  FileText,
  Users,
  CheckCheck,
  Copy,
  Check,
  Code2,
  AlertTriangle,
  ShieldCheck,
  Terminal,
} from 'lucide-react';
import type { CouncilVerdict, ReviewFinding } from '../lib/types';
import { TURTLE_CHARACTERS, getCharacterByPersona } from '../lib/characters';
import { FindingBadge } from './FindingBadge';
import { TurtleAvatar } from './TurtleAvatar';

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

  const synthesizedFindings = verdict?.synthesized ?? [];
  const personasVerdicts = verdict?.personas ?? [];
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
              Code-Turtle Council Final Review
            </h2>
            <p className="text-xs text-slate-400">
              Synthesized by Judge Shellsworth after multi-persona consensus &amp; Roslyn verification
            </p>
          </div>
        </div>

        {/* Verdict Status Indicator */}
        <div className="flex items-center gap-2">
          {hasCriticalOrError ? (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-rose-500/15 text-rose-300 border border-rose-500/30 shadow-md shadow-rose-500/10">
              <AlertTriangle className="w-4 h-4 text-rose-400" />
              Changes Required ({synthesizedFindings.length} findings)
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 shadow-md shadow-emerald-500/10">
              <ShieldCheck className="w-4 h-4 text-emerald-400" />
              Council Approved ({synthesizedFindings.length} findings)
            </span>
          )}
        </div>
      </div>

      {/* Tabs navigation */}
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
            {synthesizedFindings.length === 0 ? (
              <div className="p-8 text-center rounded-xl bg-slate-950/40 border border-slate-800 text-slate-400 text-sm">
                No findings reported by the Turtle Council. Code is exceptionally clean!
              </div>
            ) : (
              synthesizedFindings.map((finding, idx) => (
                <div
                  key={`${finding.title}-${idx}`}
                  className="p-4 rounded-xl bg-slate-950/50 border border-slate-800 hover:border-slate-700 transition-all flex flex-col space-y-2.5"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2.5">
                      <FindingBadge severity={finding.severity} />
                      <h3 className="text-sm font-bold text-slate-100">{finding.title}</h3>
                    </div>
                    {finding.location && (
                      <span className="text-xs font-mono text-slate-400 bg-slate-900 px-2 py-0.5 rounded border border-slate-800">
                        {finding.location}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-300 leading-relaxed">{finding.detail}</p>
                  {finding.citedSymbolFqns && finding.citedSymbolFqns.length > 0 && (
                    <div className="flex flex-wrap items-center gap-1.5 pt-1">
                      <span className="text-[10px] text-slate-500 font-semibold uppercase">Symbols:</span>
                      {finding.citedSymbolFqns.map((fqn) => (
                        <span
                          key={fqn}
                          className="inline-flex items-center gap-1 text-[11px] font-mono px-2 py-0.5 rounded bg-sky-950/50 text-sky-300 border border-sky-800/40"
                        >
                          <Code2 className="w-3 h-3 text-sky-400" />
                          {fqn}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              ))
            )}
          </motion.div>
        )}

        {activeTab === 'personas' && (
          <motion.div
            key="personas"
            role="tabpanel"
            data-testid="persona-breakdown-panel"
            initial={{ opacity: 0, y: 6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.2 }}
            className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
          >
            {personasVerdicts.map((pv) => {
              const char = getCharacterByPersona(pv.persona) || TURTLE_CHARACTERS.speedy;
              return (
                <div
                  key={pv.persona}
                  className="rounded-xl bg-slate-950/50 border border-slate-800 p-4 flex flex-col space-y-3"
                >
                  <div className="flex items-center gap-3 pb-3 border-b border-slate-800/70">
                    <TurtleAvatar character={char.id} size={40} state="idle" />
                    <div>
                      <h3 className="text-sm font-bold text-slate-100">{char.name}</h3>
                      <p className="text-[11px] text-slate-400">{char.title}</p>
                    </div>
                  </div>
                  <div className="flex flex-col space-y-2 max-h-80 overflow-y-auto pr-1">
                    {pv.findings.length === 0 ? (
                      <p className="text-xs text-slate-500 italic py-2">No findings from this persona.</p>
                    ) : (
                      pv.findings.map((f, fIdx) => (
                        <div key={fIdx} className="p-2.5 rounded-lg bg-slate-900/80 border border-slate-800/60 text-xs">
                          <div className="flex items-center justify-between gap-1 mb-1">
                            <span className="font-semibold text-slate-200">{f.title}</span>
                            <FindingBadge severity={f.severity} />
                          </div>
                          <p className="text-[11px] text-slate-400">{f.detail}</p>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              );
            })}
          </motion.div>
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
