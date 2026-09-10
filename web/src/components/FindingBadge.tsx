import React from 'react';
import { AlertOctagon, AlertCircle, AlertTriangle, Info, Sparkles } from 'lucide-react';
import type { Severity } from '../lib/types';

export interface FindingBadgeProps {
  severity: Severity | string;
  count?: number;
  size?: 'sm' | 'md' | 'lg';
  showIcon?: boolean;
  className?: string;
}

interface SeverityConfig {
  label: string;
  badgeClasses: string;
  dotColor: string;
  icon: React.ComponentType<{ className?: string }>;
}

const SEVERITY_CONFIGS: Record<string, SeverityConfig> = {
  critical: {
    label: 'Critical',
    badgeClasses: 'bg-rose-500/20 text-rose-300 border-rose-500/40 shadow-rose-500/10',
    dotColor: 'bg-rose-400',
    icon: AlertOctagon,
  },
  error: {
    label: 'Error',
    badgeClasses: 'bg-red-500/20 text-red-300 border-red-500/40 shadow-red-500/10',
    dotColor: 'bg-red-400',
    icon: AlertCircle,
  },
  warning: {
    label: 'Warning',
    badgeClasses: 'bg-amber-500/20 text-amber-300 border-amber-500/40 shadow-amber-500/10',
    dotColor: 'bg-amber-400',
    icon: AlertTriangle,
  },
  nit: {
    label: 'Nit',
    badgeClasses: 'bg-sky-500/20 text-sky-300 border-sky-500/40 shadow-sky-500/10',
    dotColor: 'bg-sky-400',
    icon: Sparkles,
  },
  info: {
    label: 'Info',
    badgeClasses: 'bg-slate-500/20 text-slate-300 border-slate-500/40 shadow-slate-500/10',
    dotColor: 'bg-slate-400',
    icon: Info,
  },
};

const DEFAULT_CONFIG: SeverityConfig = {
  label: 'Info',
  badgeClasses: 'bg-slate-500/20 text-slate-300 border-slate-500/40 shadow-slate-500/10',
  dotColor: 'bg-slate-400',
  icon: Info,
};

const SIZE_CLASSES = {
  sm: 'text-xs px-2 py-0.5 gap-1',
  md: 'text-xs px-2.5 py-1 gap-1.5',
  lg: 'text-sm px-3 py-1.5 gap-2',
};

const ICON_SIZES = {
  sm: 'w-3 h-3',
  md: 'w-3.5 h-3.5',
  lg: 'w-4 h-4',
};

export function FindingBadge({
  severity,
  count,
  size = 'sm',
  showIcon = true,
  className = '',
}: FindingBadgeProps) {
  const norm = (severity || '').toLowerCase();
  const config = SEVERITY_CONFIGS[norm] || DEFAULT_CONFIG;
  const IconComponent = config.icon;

  return (
    <span
      data-testid="finding-badge"
      data-severity={norm}
      className={`inline-flex items-center font-medium rounded-full border shadow-sm transition-colors ${config.badgeClasses} ${SIZE_CLASSES[size]} ${className}`}
    >
      {showIcon && <IconComponent className={`${ICON_SIZES[size]} shrink-0`} />}
      <span>{severity}</span>
      {count !== undefined && (
        <span
          data-testid="finding-badge-count"
          className="ml-0.5 px-1.5 py-0.2 rounded-full bg-white/10 text-[10px] font-bold"
        >
          {count}
        </span>
      )}
    </span>
  );
}
