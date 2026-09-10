import React from 'react';
import { TURTLE_CHARACTERS } from '../lib/characters';
import { PersonaCard } from './PersonaCard';
import type { ReviewFinding } from '../lib/types';
import type { PersonaState } from '../hooks/useDemoReplay';
import { Activity, ShieldCheck, FileSearch, Sparkles } from 'lucide-react';

export interface CouncilChamberProps {
  personaStates?: Record<string, PersonaState>;
  findings?: ReviewFinding[];
  quips?: Record<string, string>;
  title?: string;
  className?: string;
}

const COUNCIL_MEMBERS = [
  TURTLE_CHARACTERS.speedy,
  TURTLE_CHARACTERS.sheldon,
  TURTLE_CHARACTERS.sensei,
];

function matchesPersona(findingPersona: string, charRole: string, charName: string, charId: string): boolean {
  const norm = (findingPersona || '').toLowerCase();
  const roleNorm = charRole.toLowerCase();
  const nameNorm = charName.toLowerCase();
  const idNorm = charId.toLowerCase();

  if (norm === roleNorm || norm === nameNorm || norm === idNorm) return true;
  if (idNorm === 'speedy' && (norm.includes('allocation') || norm.includes('perf'))) return true;
  if (idNorm === 'sheldon' && norm.includes('security')) return true;
  if (idNorm === 'sensei' && (norm.includes('architect') || norm.includes('idiomatic'))) return true;
  return false;
}

export function CouncilChamber({
  personaStates = {},
  findings = [],
  quips = {},
  title = 'Turtle Review Council Chamber',
  className = '',
}: CouncilChamberProps) {
  const getPersonaState = (charRole: string, charId: string, charName: string): PersonaState => {
    return (
      personaStates[charRole] ||
      personaStates[charId] ||
      personaStates[charName] || {
        status: 'idle',
        findingCount: 0,
      }
    );
  };

  const getPersonaQuip = (
    charRole: string,
    charId: string,
    charName: string,
    state: PersonaState
  ): string | undefined => {
    return (
      quips[charRole] ||
      quips[charId] ||
      quips[charName] ||
      state.quip ||
      undefined
    );
  };

  // Aggregate stats
  const totalFindings = findings.length;
  const activeCount = COUNCIL_MEMBERS.filter((m) => {
    const s = getPersonaState(m.role, m.id, m.name);
    return s.status === 'running' || s.status === 'completed';
  }).length;
  const completedCount = COUNCIL_MEMBERS.filter((m) => {
    const s = getPersonaState(m.role, m.id, m.name);
    return s.status === 'completed';
  }).length;

  return (
    <section
      data-testid="council-chamber"
      className={`w-full max-w-7xl mx-auto flex flex-col space-y-6 ${className}`}
      aria-label={title}
    >
      {/* Chamber Header & Aggregate Statistics Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 rounded-xl border border-slate-800/80 bg-slate-900/60 backdrop-blur-md">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
            <Sparkles className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-lg font-bold text-slate-100 tracking-tight flex items-center gap-2">
              {title}
            </h2>
            <p className="text-xs text-slate-400">
              Three autonomous personas analyzing performance, security, and architecture
            </p>
          </div>
        </div>

        {/* Aggregate Stats Metrics */}
        <div className="flex items-center gap-4 flex-wrap">
          <div
            data-testid="council-stats-total"
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60"
          >
            <FileSearch className="w-4 h-4 text-emerald-400" />
            <div className="text-xs">
              <span className="text-slate-400">Total Findings: </span>
              <span className="font-bold text-slate-100 font-mono">{totalFindings}</span>
            </div>
          </div>

          <div
            data-testid="council-stats-active"
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60"
          >
            <Activity className="w-4 h-4 text-amber-400" />
            <div className="text-xs">
              <span className="text-slate-400">Active Personas: </span>
              <span className="font-bold text-slate-100 font-mono">
                {activeCount} / {COUNCIL_MEMBERS.length}
              </span>
            </div>
          </div>

          <div
            data-testid="council-stats-completed"
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60"
          >
            <ShieldCheck className="w-4 h-4 text-sky-400" />
            <div className="text-xs">
              <span className="text-slate-400">Finished: </span>
              <span className="font-bold text-slate-100 font-mono">
                {completedCount} / {COUNCIL_MEMBERS.length}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Responsive Grid displaying the 3 Council Member Cards side-by-side */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 items-stretch">
        {COUNCIL_MEMBERS.map((member) => {
          const state = getPersonaState(member.role, member.id, member.name);
          const personaFindings = findings.filter((f) =>
            matchesPersona(f.persona, member.role, member.name, member.id)
          );
          const quip = getPersonaQuip(member.role, member.id, member.name, state);

          return (
            <PersonaCard
              key={member.id}
              character={member}
              status={state.status}
              quip={quip}
              findings={personaFindings}
              isFocused={state.status === 'running'}
              className="h-full"
            />
          );
        })}
      </div>
    </section>
  );
}
