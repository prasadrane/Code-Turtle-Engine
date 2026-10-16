import React from 'react';
import {
  ShieldAlert,
  Zap,
  Lock,
  Cpu,
  Layers,
  Database,
  KeyRound,
  FolderLock,
  RefreshCcw,
  Boxes,
  CodeXml,
  AlertOctagon,
} from 'lucide-react';

export interface RiskTagBadgeProps {
  tag: string;
  className?: string;
}

interface RiskTagConfig {
  label: string;
  badgeClasses: string;
  icon: React.ComponentType<{ className?: string }>;
}

const RISK_CONFIGS: Record<string, RiskTagConfig> = {
  'sql-injection': {
    label: 'SQL Injection',
    badgeClasses: 'bg-rose-500/15 text-rose-300 border-rose-500/30',
    icon: Database,
  },
  'path-traversal': {
    label: 'Path Traversal',
    badgeClasses: 'bg-rose-500/15 text-rose-300 border-rose-500/30',
    icon: FolderLock,
  },
  'hardcoded-secret': {
    label: 'Hardcoded Secret',
    badgeClasses: 'bg-red-500/15 text-red-300 border-red-500/30',
    icon: KeyRound,
  },
  'sync-over-async': {
    label: 'Sync-Over-Async',
    badgeClasses: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
    icon: RefreshCcw,
  },
  'missing-configureawait': {
    label: 'Missing ConfigureAwait',
    badgeClasses: 'bg-yellow-500/15 text-yellow-300 border-yellow-500/30',
    icon: Zap,
  },
  'unawaited-task': {
    label: 'Fire-And-Forget Task',
    badgeClasses: 'bg-orange-500/15 text-orange-300 border-orange-500/30',
    icon: AlertOctagon,
  },
  'captive-dependency': {
    label: 'Captive Dependency',
    badgeClasses: 'bg-purple-500/15 text-purple-300 border-purple-500/30',
    icon: Layers,
  },
  'service-locator': {
    label: 'Service Locator',
    badgeClasses: 'bg-indigo-500/15 text-indigo-300 border-indigo-500/30',
    icon: Boxes,
  },
  'swallowed-exception': {
    label: 'Swallowed Exception',
    badgeClasses: 'bg-rose-500/15 text-rose-300 border-rose-500/30',
    icon: ShieldAlert,
  },
  'thread-unsafe-collection': {
    label: 'Thread-Unsafe Cache',
    badgeClasses: 'bg-red-500/15 text-red-300 border-red-500/30',
    icon: Cpu,
  },
  'boxing-allocation': {
    label: 'Implicit Boxing',
    badgeClasses: 'bg-cyan-500/15 text-cyan-300 border-cyan-500/30',
    icon: Boxes,
  },
  'linq-closure': {
    label: 'LINQ Closure Capture',
    badgeClasses: 'bg-sky-500/15 text-sky-300 border-sky-500/30',
    icon: CodeXml,
  },
};

export function detectRiskTag(title: string, detail: string): string | null {
  const text = `${title} ${detail}`.toLowerCase();
  if (text.includes('sql') || text.includes('injection')) return 'sql-injection';
  if (text.includes('path') && (text.includes('traversal') || text.includes('combine'))) return 'path-traversal';
  if (text.includes('secret') || text.includes('api key') || text.includes('credential')) return 'hardcoded-secret';
  if (text.includes('sync-over-async') || text.includes('.result') || text.includes('.wait()')) return 'sync-over-async';
  if (text.includes('configureawait')) return 'missing-configureawait';
  if (text.includes('fire-and-forget') || (text.includes('unawaited') && text.includes('task'))) return 'unawaited-task';
  if (text.includes('captive') && text.includes('dependenc')) return 'captive-dependency';
  if (text.includes('service locator') || text.includes('iserviceprovider')) return 'service-locator';
  if (text.includes('swallowed') || (text.includes('catch') && text.includes('exception'))) return 'swallowed-exception';
  if (text.includes('thread-unsafe') || (text.includes('dictionary') && text.includes('concurrent'))) return 'thread-unsafe-collection';
  if (text.includes('boxing')) return 'boxing-allocation';
  if (text.includes('closure')) return 'linq-closure';
  return null;
}

export function RiskTagBadge({ tag, className = '' }: RiskTagBadgeProps) {
  const config = RISK_CONFIGS[tag] || {
    label: tag,
    badgeClasses: 'bg-slate-500/15 text-slate-300 border-slate-500/30',
    icon: Lock,
  };
  const Icon = config.icon;

  return (
    <span
      data-testid="risk-tag-badge"
      data-tag={tag}
      className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-mono font-bold uppercase tracking-wider border shadow-sm ${config.badgeClasses} ${className}`}
    >
      <Icon className="w-3 h-3 shrink-0" />
      <span>{config.label}</span>
    </span>
  );
}
