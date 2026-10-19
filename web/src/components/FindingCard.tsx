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
      className={`finding-card ${className}`}
    >
      <div className="finding-card-heading">
        <div className="finding-title-row">
          <FindingBadge severity={finding.severity} />
          {riskTag && <RiskTagBadge tag={riskTag} />}
          <h3>{finding.title}</h3>
        </div>
        {finding.location && (
          <button
            type="button"
            onClick={handleCopyLocation}
            title="Click to copy file and line location"
            className="finding-location"
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

      <p className="finding-detail">{finding.detail}</p>
      <p className="finding-caveat">Recommendation and location are not verified.
        {!finding.citedSymbolFqns?.length && ' Advisory finding without citations.'}
        {finding.citedSymbolFqns?.some(fqn => getAuditStatus(fqn) === null) && ' Provisional / legacy evidence unavailable.'}
      </p>

      {finding.citedSymbolFqns && finding.citedSymbolFqns.length > 0 && (
        <div className="finding-citations">
          <span className="citation-label">
            Citation existence:
          </span>
          {finding.citedSymbolFqns.map((fqn) => {
            const isVerified = getAuditStatus(fqn);
            return (
              <span
                key={fqn}
                data-testid="symbol-provenance-chip"
                data-verified={isVerified === true}
                className={`symbol-chip ${
                  isVerified === false
                    ? 'evidence-rejected line-through'
                    : 'evidence-supported'
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
