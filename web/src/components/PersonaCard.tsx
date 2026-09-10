import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { CheckCircle2, Loader2, AlertTriangle, Clock } from 'lucide-react';
import { TurtleAvatar, TurtleAvatarState } from './TurtleAvatar';
import { FindingBadge } from './FindingBadge';
import {
  TurtleCharacter,
  TURTLE_CHARACTERS,
  getCharacterByPersona,
  getCharacterByName,
} from '../lib/characters';
import type { ReviewFinding } from '../lib/types';

export interface PersonaCardProps {
  character: TurtleCharacter | string;
  status?: 'idle' | 'running' | 'thinking' | 'completed' | 'failed';
  quip?: string | null;
  findings?: ReviewFinding[];
  className?: string;
  isFocused?: boolean;
}

function resolveCharacter(char: TurtleCharacter | string): TurtleCharacter {
  if (typeof char !== 'string') return char;
  const lower = char.toLowerCase();
  if (TURTLE_CHARACTERS[lower]) return TURTLE_CHARACTERS[lower];
  const byRole = getCharacterByPersona(char);
  if (byRole) return byRole;
  const byName = getCharacterByName(char);
  if (byName) return byName;
  return TURTLE_CHARACTERS.speedy;
}

export function PersonaCard({
  character,
  status = 'idle',
  quip,
  findings = [],
  className = '',
  isFocused = false,
}: PersonaCardProps) {
  const char = resolveCharacter(character);
  const avatarState: TurtleAvatarState =
    status === 'running'
      ? findings.length > 0
        ? 'finding'
        : 'thinking'
      : (status as TurtleAvatarState);

  const getStatusBadge = () => {
    switch (status) {
      case 'running':
      case 'thinking':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/40 animate-pulse">
            <Loader2 className="w-3 h-3 animate-spin shrink-0" />
            Analyzing...
          </span>
        );
      case 'completed':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">
            <CheckCircle2 className="w-3 h-3 shrink-0" />
            {`Completed (${findings.length})`}
          </span>
        );
      case 'failed':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/40">
            <AlertTriangle className="w-3 h-3 shrink-0" />
            Failed
          </span>
        );
      case 'idle':
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-700/50 text-slate-400 border border-slate-600/40">
            <Clock className="w-3 h-3 shrink-0" />
            Idle
          </span>
        );
    }
  };

  return (
    <div
      data-testid={`persona-card-${char.id}`}
      className={`relative flex flex-col rounded-xl border bg-slate-900/90 p-5 shadow-xl backdrop-blur-sm transition-all duration-300 ${
        isFocused ? 'ring-2 ring-emerald-400/60 shadow-emerald-950/40' : ''
      } ${char.color.border} ${char.color.glow} ${className}`}
    >
      {/* Top Banner Header */}
      <div className="flex items-start gap-4 pb-4 border-b border-slate-800">
        <div className="relative">
          <TurtleAvatar character={char} state={avatarState} size={72} />
          <span className="absolute -bottom-1 -right-1 text-lg select-none">
            {char.icon}
          </span>
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between gap-2 flex-wrap mb-1">
            <h3 className="font-bold text-lg text-slate-100 truncate flex items-center gap-1.5">
              {char.name}
            </h3>
            {getStatusBadge()}
          </div>
          <p className="text-xs text-slate-400 truncate mb-1.5">{char.title}</p>
          <div className="flex flex-wrap gap-1">
            {char.focusAreas.slice(0, 3).map((area) => (
              <span
                key={area}
                className="px-1.5 py-0.5 rounded text-[10px] bg-slate-800/80 text-slate-400 border border-slate-700/50"
              >
                {area}
              </span>
            ))}
          </div>
        </div>
      </div>

      {/* Animated Speech Bubble */}
      <div className="my-3 min-h-[52px] flex items-center">
        <AnimatePresence mode="wait">
          {quip && (
            <motion.div
              key={quip}
              data-testid="speech-bubble"
              initial={{ opacity: 0, y: -6, scale: 0.96 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: 4, scale: 0.96 }}
              transition={{ duration: 0.2 }}
              className="relative w-full rounded-lg bg-slate-800/95 border border-slate-700/80 p-3 shadow-md"
            >
              <div
                className="absolute -top-1.5 left-7 w-3 h-3 rotate-45 bg-slate-800 border-t border-l border-slate-700/80"
                aria-hidden="true"
              />
              <p className="text-xs italic text-slate-200 leading-relaxed">
                <span className="opacity-60 select-none mr-0.5">&ldquo;</span>
                <span>{quip}</span>
                <span className="opacity-60 select-none ml-0.5">&rdquo;</span>
              </p>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Findings Section */}
      <div className="flex-1 flex flex-col min-h-0">
        <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-800/60">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400">
            Persona Findings
          </h4>
          <span className="text-xs text-slate-500 font-mono">
            {findings.length} item{findings.length === 1 ? '' : 's'}
          </span>
        </div>

        {findings.length === 0 ? (
          <div className="flex-1 flex flex-col items-center justify-center p-6 text-center text-slate-500 rounded-lg border border-dashed border-slate-800/80 bg-slate-950/30">
            <p className="text-xs">
              {status === 'running' || status === 'thinking'
                ? 'Analyzing codebase for patterns...'
                : 'No findings yet.'}
            </p>
          </div>
        ) : (
          <div className="flex-1 overflow-y-auto max-h-64 space-y-2.5 pr-1.5">
            {findings.map((finding, idx) => (
              <motion.div
                key={`${finding.location}-${idx}`}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.2, delay: idx * 0.05 }}
                className="rounded-lg border border-slate-800 bg-slate-950/50 p-3 hover:border-slate-700 transition-colors"
              >
                <div className="flex items-start justify-between gap-2 mb-1.5">
                  <FindingBadge severity={finding.severity} size="sm" />
                  <span className="text-[11px] font-mono text-slate-400 truncate max-w-[140px]" title={finding.location}>
                    {finding.location}
                  </span>
                </div>
                <h5 className="text-xs font-semibold text-slate-200 mb-1 leading-snug">
                  {finding.title}
                </h5>
                <p className="text-xs text-slate-400 line-clamp-2 leading-relaxed mb-2">
                  {finding.detail}
                </p>
                {finding.citedSymbolFqns && finding.citedSymbolFqns.length > 0 && (
                  <div className="flex flex-wrap gap-1 pt-1 border-t border-slate-800/50">
                    {finding.citedSymbolFqns.map((fqn) => (
                      <span
                        key={fqn}
                        className="px-1.5 py-0.5 rounded text-[10px] font-mono bg-slate-900 border border-slate-700/60 text-emerald-400/90"
                        title={fqn}
                      >
                        {fqn}
                      </span>
                    ))}
                  </div>
                )}
              </motion.div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
