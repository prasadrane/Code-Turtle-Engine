import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Check, Loader2, Sparkles } from 'lucide-react';

export interface TimelinePhase {
  id: string;
  label: string;
  description: string;
}

export const TIMELINE_PHASES: TimelinePhase[] = [
  { id: 'validating', label: 'Validating', description: 'Checking PR accessibility' },
  { id: 'cloning', label: 'Cloning', description: 'Fetching repository' },
  { id: 'compiling', label: 'Compiling', description: 'Roslyn AST parsing' },
  { id: 'deliberating', label: 'Deliberating', description: 'Council review in parallel' },
  { id: 'auditing', label: 'Auditing', description: 'Roslyn citation verification' },
  { id: 'complete', label: 'Complete', description: 'Synthesized report ready' },
];

export interface PhaseTimelineProps {
  currentPhase: string;
  phaseMessage?: string;
  orientation?: 'horizontal' | 'vertical';
  className?: string;
}

function normalizePhase(phase: string): string {
  const norm = (phase || '').toLowerCase().trim();
  if (norm === 'completed') return 'complete';
  if (norm === 'restoring') return 'cloning';
  if (norm === 'persona_execution' || norm === 'deliberation') return 'deliberating';
  if (norm === 'guard_audit' || norm === 'audit') return 'auditing';
  return norm;
}

function getPhaseIndex(phase: string): number {
  const normalized = normalizePhase(phase);
  const idx = TIMELINE_PHASES.findIndex((p) => p.id === normalized);
  return idx !== -1 ? idx : 0;
}

export function PhaseTimeline({
  currentPhase,
  phaseMessage,
  orientation = 'horizontal',
  className = '',
}: PhaseTimelineProps) {
  const activeIndex = getPhaseIndex(currentPhase);
  const isAllComplete = normalizePhase(currentPhase) === 'complete';
  const isVertical = orientation === 'vertical';

  return (
    <div
      data-testid="phase-timeline"
      data-orientation={orientation}
      className={`w-full rounded-2xl bg-slate-900/80 border border-slate-800/80 p-5 shadow-xl backdrop-blur-md flex flex-col gap-4 ${className}`}
    >
      {/* Stepper container */}
      <div
        className={`flex ${
          isVertical ? 'flex-col space-y-6' : 'flex-row items-center justify-between'
        } relative w-full overflow-x-auto`}
      >
        {TIMELINE_PHASES.map((phase, idx) => {
          const isCompleted = isAllComplete || idx < activeIndex;
          const isActive = !isAllComplete && idx === activeIndex;
          const isPending = !isAllComplete && idx > activeIndex;

          const status = isCompleted ? 'completed' : isActive ? 'active' : 'pending';

          return (
            <div
              key={phase.id}
              data-testid={`phase-step-${phase.id}`}
              data-status={status}
              className={`flex ${
                isVertical ? 'flex-row items-center space-x-4' : 'flex-col items-center text-center flex-1'
              } relative z-10`}
            >
              {/* Connector line for horizontal layout */}
              {!isVertical && idx < TIMELINE_PHASES.length - 1 && (
                <div
                  className="absolute top-4 left-1/2 w-full h-0.5 -z-10"
                  style={{ transform: 'translateY(-50%)' }}
                >
                  <div
                    className={`h-full transition-colors duration-500 ${
                      idx < activeIndex || isAllComplete ? 'bg-emerald-500' : 'bg-slate-700/60'
                    }`}
                  />
                </div>
              )}

              {/* Step indicator node */}
              <motion.div
                layout
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold text-xs transition-all duration-300 ${
                  isCompleted
                    ? 'bg-emerald-500 text-slate-950 shadow-md shadow-emerald-500/30'
                    : isActive
                    ? 'bg-sky-500 text-slate-950 ring-4 ring-sky-500/20 shadow-md shadow-sky-500/40 animate-pulse'
                    : 'bg-slate-800 text-slate-400 border border-slate-700'
                }`}
              >
                {isCompleted ? (
                  <Check className="w-4 h-4 stroke-[3]" />
                ) : isActive ? (
                  <Loader2 className="w-4 h-4 animate-spin text-slate-950" />
                ) : (
                  <span>{idx + 1}</span>
                )}
              </motion.div>

              {/* Step label and description */}
              <div className={`${isVertical ? 'text-left' : 'mt-2'} flex flex-col items-center`}>
                <span
                  className={`text-xs font-medium tracking-wide ${
                    isActive
                      ? 'text-sky-400 font-bold'
                      : isCompleted
                      ? 'text-slate-200'
                      : 'text-slate-500'
                  }`}
                >
                  {phase.label}
                </span>
                <span className="hidden sm:inline-block text-[10px] text-slate-500 mt-0.5">
                  {phase.description}
                </span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Live Phase Message Bar */}
      {phaseMessage && (
        <AnimatePresence mode="wait">
          <motion.div
            key={phaseMessage}
            initial={{ opacity: 0, y: 4 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -4 }}
            transition={{ duration: 0.2 }}
            className="flex items-center gap-2.5 px-3.5 py-2 rounded-lg bg-slate-800/60 border border-slate-700/50 text-xs text-slate-300 font-mono"
          >
            <Sparkles className="w-3.5 h-3.5 text-sky-400 flex-shrink-0 animate-pulse" />
            <span className="truncate">{phaseMessage}</span>
          </motion.div>
        </AnimatePresence>
      )}
    </div>
  );
}
