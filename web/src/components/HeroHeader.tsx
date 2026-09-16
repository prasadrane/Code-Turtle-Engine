import React from 'react';
import { ShieldCheck, Cpu, Zap, Radio, Sparkles } from 'lucide-react';
import { TURTLE_CHARACTERS } from '../lib/characters';
import { TurtleAvatar } from './TurtleAvatar';

export interface HeroHeaderProps {
  className?: string;
}

export function HeroHeader({ className = '' }: HeroHeaderProps) {
  const characters = [
    TURTLE_CHARACTERS.speedy,
    TURTLE_CHARACTERS.sheldon,
    TURTLE_CHARACTERS.sensei,
    TURTLE_CHARACTERS.judge,
  ];

  return (
    <header className={`w-full max-w-4xl mx-auto text-center flex flex-col items-center space-y-6 pt-4 pb-2 ${className}`}>
      {/* Shell Badge */}
      <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-xs font-semibold tracking-wide uppercase shadow-sm">
        <Sparkles className="w-3.5 h-3.5 text-emerald-400 animate-pulse" />
        <span>Code-Turtle Live &bull; AI Review Council</span>
      </div>

      {/* Main Title */}
      <div className="space-y-3">
        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight text-slate-100 font-mono">
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400">
            Code-Turtle
          </span>{' '}
          Council
        </h1>
        <p className="text-lg sm:text-xl text-slate-300 font-medium max-w-2xl mx-auto leading-relaxed">
          Three expert AI reviewer turtles. One Roslyn-verified pull request synthesis. Zero hallucinations.
        </p>
      </div>

      {/* Mini character avatars row */}
      <div className="flex flex-wrap items-center justify-center gap-2.5 pt-1">
        {characters.map((char) => (
          <div
            key={char.id}
            className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-slate-900/80 border border-slate-800 shadow-sm hover:border-slate-700 transition-colors"
            title={`${char.name}: ${char.title}`}
          >
            <TurtleAvatar character={char} size="sm" />
            <span className="text-xs font-medium text-slate-300 whitespace-nowrap">{char.name}</span>
          </div>
        ))}
      </div>

      {/* Value Proposition Pills */}
      <div className="flex flex-wrap items-center justify-center gap-2 sm:gap-3 text-xs text-slate-400 font-mono">
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-md bg-slate-900/60 border border-slate-800">
          <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
          Roslyn Citation Guard
        </span>
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-md bg-slate-900/60 border border-slate-800">
          <Cpu className="w-3.5 h-3.5 text-amber-400" />
          Parallel Council
        </span>
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-md bg-slate-900/60 border border-slate-800">
          <Zap className="w-3.5 h-3.5 text-teal-400" />
          Zero Hallucinations
        </span>
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-md bg-slate-900/60 border border-slate-800">
          <Radio className="w-3.5 h-3.5 text-cyan-400" />
          Live SSE Streaming
        </span>
      </div>
    </header>
  );
}
