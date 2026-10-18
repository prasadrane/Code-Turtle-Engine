import React from 'react';
import { motion } from 'framer-motion';
import type { PersonaVerdict, GuardResult, ReviewFinding } from '../lib/types';
import { TURTLE_CHARACTERS, getCharacterByPersona } from '../lib/characters';
import { FindingCard } from './FindingCard';
import { TurtleAvatar } from './TurtleAvatar';

export interface PersonaBreakdownPanelProps {
  personasVerdicts: PersonaVerdict[];
  guardAudit?: GuardResult[];
  retained?: ReviewFinding[];
}

export function PersonaBreakdownPanel({ personasVerdicts, guardAudit = [], retained = [] }: PersonaBreakdownPanelProps) {
  return (
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
                  <div key={fIdx}>
                    <p className="text-xs text-amber-300">Raw persona audit view — {retained.some(r => r.title === f.title && r.location === f.location && r.persona === f.persona) ? 'retained; citations may be stripped' : 'not retained after arbitration / guard'}</p>
                    <FindingCard finding={f} guardAudit={guardAudit} />
                  </div>
                ))
              )}
            </div>
          </div>
        );
      })}
    </motion.div>
  );
}
