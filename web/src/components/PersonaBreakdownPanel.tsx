import React from 'react';
import { motion } from 'framer-motion';
import type { PersonaVerdict } from '../lib/types';
import { TURTLE_CHARACTERS, getCharacterByPersona } from '../lib/characters';
import { FindingBadge } from './FindingBadge';
import { TurtleAvatar } from './TurtleAvatar';

export interface PersonaBreakdownPanelProps {
  personasVerdicts: PersonaVerdict[];
}

export function PersonaBreakdownPanel({ personasVerdicts }: PersonaBreakdownPanelProps) {
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
  );
}
