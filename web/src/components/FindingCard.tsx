import React, { useState } from 'react';
import {
  Code2,
  CheckCircle2,
  XCircle,
  Copy,
  Check,
} from 'lucide-react';
import type { ReviewFinding, GuardResult } from '../lib/types';
import { FindingBadge } from './FindingBadge';
import { RiskTagBadge, detectRiskTag } from './RiskTagBadge';

export interface FindingCardProps {
  finding: ReviewFinding;
  guardAudit?: GuardResult[];
  className?: string;
}

export function FindingCard({ finding, guardAudit = [], className = '' }: FindingCardProps) {
  const [copiedLoc, setCopiedLoc] = useState(false);
  const riskTag = detectRiskTag(finding.title, finding.detail);

  const handleCopyLocation = async (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!finding.location) return;
    try {
      await navigator.clipboard.writeText(finding.location);
      setCopiedLoc(true);
      setTimeout(() => setCopiedLoc(false), 1500);
    } catch {
      // ignore
    }
  };

  const getAuditStatus = (fqn: string): boolean | null => {
    const item = guardAudit.find((a) => a.citedFqn === fqn);
    return item?.evidence === 'Payload' || item?.evidence === 'Compilation' ? item.verified : item?.verified === false ? false : null;
  };

  return (
    <div
      data-testid="finding-card"
      className={`p-4 rounded-xl bg-slate-950/50 border border-slate-800 hover:border-slate-700 transition-all flex flex-col space-y-2.5 ${className}`}
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <FindingBadge severity={finding.severity} />
          {riskTag && <RiskTagBadge tag={riskTag} />}
          <h3 className="text-sm font-bold text-slate-100">{finding.title}</h3>
        </div>
        {finding.location && (
          <button
            type="button"
            onClick={handleCopyLocation}
            title="Click to copy file and line location"
            className="group inline-flex items-center gap-1.5 text-xs font-mono text-slate-400 hover:text-sky-300 bg-slate-900 hover:bg-slate-850 px-2.5 py-1 rounded-md border border-slate-800 hover:border-sky-700 transition-all cursor-pointer"
          >
            <span>{finding.location}</span>
            {copiedLoc ? (
              <Check className="w-3 h-3 text-emerald-400" />
            ) : (
              <Copy className="w-3 h-3 opacity-60 group-hover:opacity-100" />
            )}
          </button>
        )}
      </div>

      <p className="text-xs text-slate-300 leading-relaxed">{finding.detail}</p>
      <p className="text-[10px] text-slate-500">Recommendation and location are not verified.
        {!finding.citedSymbolFqns?.length && ' Advisory finding without citations.'}
        {finding.citedSymbolFqns?.some(fqn => getAuditStatus(fqn) === null) && ' Provisional / legacy evidence unavailable.'}
      </p>

      {finding.citedSymbolFqns && finding.citedSymbolFqns.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 pt-1 border-t border-slate-900">
          <span className="text-[10px] text-slate-500 font-semibold uppercase tracking-wider">
            Citation existence:
          </span>
          {finding.citedSymbolFqns.map((fqn) => {
            const isVerified = getAuditStatus(fqn);
            return (
              <span
                key={fqn}
                data-testid="symbol-provenance-chip"
                data-verified={isVerified === true}
                className={`inline-flex items-center gap-1 text-[11px] font-mono px-2 py-0.5 rounded border transition-colors ${
                  isVerified === false
                    ? 'bg-rose-950/40 text-rose-300 border-rose-800/40 line-through'
                    : 'bg-sky-950/50 text-sky-300 border-sky-800/40'
                }`}
              >
                {isVerified === true ? (
                  <CheckCircle2 className="w-3 h-3 text-emerald-400" />
                ) : isVerified === false ? (
                  <XCircle className="w-3 h-3 text-rose-400" />
                ) : (
                  <Code2 className="w-3 h-3 text-sky-400" />
                )}
                <span>{fqn}</span>
                {isVerified === true && (
                  <span className="text-[9px] text-emerald-400 font-sans font-semibold">
                    ✓ Roslyn ({guardAudit.find(a => a.citedFqn === fqn)?.evidence})
                  </span>
                )}
              </span>
            );
          })}
        </div>
      )}
    </div>
  );
}
