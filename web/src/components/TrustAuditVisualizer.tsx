import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  ShieldCheck,
  ShieldAlert,
  Search,
  CheckCircle2,
  XCircle,
  HelpCircle,
  Info,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import type { GuardResult } from '../lib/types';
import { TURTLE_CHARACTERS, type TurtleCharacter } from '../lib/characters';
import { TurtleAvatar } from './TurtleAvatar';

export interface TrustAuditVisualizerProps {
  guardAudit?: GuardResult[];
  character?: TurtleCharacter;
  className?: string;
  title?: string;
}

export function TrustAuditVisualizer({
  guardAudit = [],
  character = TURTLE_CHARACTERS.judge,
  className = '',
  title = 'Roslyn Trust Audit & Citation Guard',
}: TrustAuditVisualizerProps) {
  const [showExplainer, setShowExplainer] = useState(true);

  const totalCount = guardAudit.length;
  const supported = (item: GuardResult) => item.verified && (item.evidence === 'Payload' || item.evidence === 'Compilation');
  const verifiedCount = guardAudit.filter(supported).length;
  const unknownCount = guardAudit.filter(item => item.verified && !supported(item)).length;
  const strippedCount = guardAudit.filter(item => !item.verified).length;
  const trustScore = totalCount > 0 ? Math.round((verifiedCount / totalCount) * 100) : 0;
  const isClean = totalCount > 0 && strippedCount === 0 && unknownCount === 0;

  return (
    <section
      data-testid="trust-audit-visualizer"
      className={`w-full max-w-7xl mx-auto flex flex-col space-y-6 rounded-2xl bg-slate-900/90 border border-slate-800/80 p-6 shadow-2xl backdrop-blur-md ${className}`}
      aria-label={title}
    >
      {/* Header section with Judge Shellsworth & Trust Score Shield */}
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-6 pb-6 border-b border-slate-800/80">
        <div className="flex items-center gap-4">
          <div className="relative">
            <TurtleAvatar
              character="judge"
              state={strippedCount > 0 ? 'finding' : 'completed'}
              size={68}
              className="rounded-xl border border-sky-500/30 bg-slate-800/80 p-1"
            />
            <div className="absolute -bottom-1 -right-1 p-1 rounded-full bg-sky-500 text-slate-950 shadow-md">
              <Search className="w-3.5 h-3.5 stroke-[2.5]" />
            </div>
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold text-slate-100 tracking-tight">{title}</h2>
              <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-sky-500/20 text-sky-300 border border-sky-500/30">
                Citation evidence
              </span>
            </div>
            <p className="text-xs text-slate-400 mt-1">
              Presided by <span className="text-sky-300 font-semibold">{character.name}</span> — Compiler-verifying every symbol citation.
            </p>
          </div>
        </div>

        {/* Animated Trust Score Shield Badge */}
        <div
          data-testid="trust-score-badge"
          className="flex items-center gap-4 px-5 py-3 rounded-xl bg-slate-800/90 border border-slate-700/80 shadow-lg"
        >
          <div className="relative flex items-center justify-center">
            {isClean ? (
              <ShieldCheck className="w-10 h-10 text-emerald-400" />
            ) : (
              <ShieldAlert className="w-10 h-10 text-amber-400" />
            )}
            <span className="sr-only">Trust Shield</span>
          </div>

          <div className="flex flex-col">
            <div className="flex items-baseline gap-1.5">
              <span className="text-2xl font-black font-mono text-slate-100">{trustScore}%</span>
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Citation existence rate</span>
            </div>
            <div className="flex items-center gap-3 mt-1 text-xs">
              <span className="text-emerald-400 font-medium">
                {verifiedCount} Verified
              </span>
              <span className="text-slate-600">•</span>
              <span className={`${strippedCount > 0 ? 'text-rose-400 font-bold' : 'text-slate-400'}`}>
                {strippedCount} Rejected; {unknownCount} evidence unavailable
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Roslyn AST Explainer Card */}
      <div
        data-testid="roslyn-explainer"
        className="rounded-xl bg-sky-950/30 border border-sky-800/40 p-4 transition-all"
      >
        <div
          className="flex items-center justify-between cursor-pointer"
          onClick={() => setShowExplainer(!showExplainer)}
        >
          <div className="flex items-center gap-2.5">
            <Info className="w-4 h-4 text-sky-400" />
            <h3 className="text-sm font-semibold text-sky-200">
              Roslyn compiler citation existence checks
            </h3>
          </div>
          <button
            type="button"
            className="text-sky-400 hover:text-sky-200 p-1"
            aria-label={showExplainer ? 'Hide explanation' : 'Show explanation'}
          >
            {showExplainer ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
        </div>

        <AnimatePresence>
          {showExplainer && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.2 }}
              className="overflow-hidden"
            >
              <p className="text-xs text-sky-300/80 mt-2.5 leading-relaxed">
                Citation existence checks distinguish payload support from broader compilation existence. Strip mode removes rejected citations; Flag mode retains them for inspection. Existence does not verify overloads, locations, relevance, or recommendation correctness.
              </p>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Symbol Verification Table */}
      <div className="flex flex-col space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold text-slate-200 uppercase tracking-wider">
            Symbol Verification Table ({totalCount})
          </h3>
          <span className="text-xs text-slate-400">
            {isClean
              ? 'Citation existence supported by audit evidence'
              : 'Review citation evidence below; absent evidence is not verification'}
          </span>
        </div>

        <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-950/60">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-900/80 text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-800">
              <tr>
                <th scope="col" className="px-4 py-3">Cited Symbol (FQN)</th>
                <th scope="col" className="px-4 py-3">Compiler Status</th>
                <th scope="col" className="px-4 py-3">Guard Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {guardAudit.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-4 py-6 text-center text-slate-500 italic">
                    No cited symbols evaluated yet.
                  </td>
                </tr>
              ) : (
                guardAudit.map((item, index) => {
                  return (
                    <motion.tr
                      key={`${item.citedFqn}-${index}`}
                      initial={{ opacity: 0, y: 4 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ duration: 0.2, delay: index * 0.04 }}
                      className={`hover:bg-slate-900/40 transition-colors ${
                        !item.verified ? 'bg-rose-950/20' : ''
                      }`}
                    >
                      <td className="px-4 py-3 font-mono font-medium">
                        <span
                          className={`inline-block ${
                            item.verified ? 'text-slate-200' : 'text-rose-400 line-through'
                          }`}
                        >
                          {item.citedFqn}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        {supported(item) ? (
                          <span
                            data-testid="status-verified"
                            className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30"
                          >
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            Verified ({item.evidence})
                          </span>
                        ) : (
                          <span
                            data-testid="status-stripped"
                            className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/30"
                          >
                            <XCircle className="w-3.5 h-3.5" />
                            {item.verified ? 'Legacy / evidence unavailable' : 'Citation Rejected'}
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`text-[11px] font-medium ${
                            item.verified ? 'text-slate-400' : 'text-rose-300 font-semibold'
                          }`}
                        >
                          {supported(item) ? 'Existence only; retention depends on finding policy' : item.verified ? 'No current verification claim' : 'Strip removes; Flag retains'}
                        </span>
                      </td>
                    </motion.tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
