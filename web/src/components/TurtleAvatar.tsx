import React from 'react';
import { motion, type TargetAndTransition } from 'framer-motion';
import type { TurtleCharacter } from '../lib/characters';

export type TurtleAvatarState =
  | 'idle'
  | 'thinking'
  | 'running'
  | 'finding'
  | 'completed'
  | 'failed';

export interface TurtleAvatarProps {
  character: string | TurtleCharacter;
  state?: TurtleAvatarState;
  size?: number | string;
  className?: string;
}

function resolveCharacterId(char: string | TurtleCharacter): string {
  if (typeof char !== 'string') return char.id.toLowerCase();
  const lower = char.toLowerCase();
  if (lower.includes('speedy') || lower.includes('allocation') || lower.includes('perf')) return 'speedy';
  if (lower.includes('sheldon') || lower.includes('security')) return 'sheldon';
  if (lower.includes('sensei') || lower.includes('architect') || lower.includes('idiomatic')) return 'sensei';
  if (lower.includes('judge') || lower.includes('arbiter') || lower.includes('shellsworth')) return 'judge';
  return 'speedy';
}

const ANIMATION_VARIANTS: Record<TurtleAvatarState, { animate: TargetAndTransition }> = {
  idle: {
    animate: {
      y: [0, -3, 0],
      scale: [1, 1.02, 1],
      transition: { repeat: Infinity, duration: 3, ease: 'easeInOut' },
    },
  },
  thinking: {
    animate: {
      rotate: [-2, 2, -2],
      scale: [1, 1.05, 1],
      y: [0, -2, 0],
      transition: { repeat: Infinity, duration: 0.8, ease: 'easeInOut' },
    },
  },
  running: {
    animate: {
      rotate: [-2, 2, -2],
      scale: [1, 1.05, 1],
      y: [0, -2, 0],
      transition: { repeat: Infinity, duration: 0.8, ease: 'easeInOut' },
    },
  },
  finding: {
    animate: {
      y: [0, -8, 0],
      rotate: [-4, 4, -4],
      scale: [1, 1.12, 1],
      transition: { repeat: Infinity, duration: 0.5, ease: 'backOut' },
    },
  },
  completed: {
    animate: {
      y: [0, -5, 0],
      rotate: [0, -4, 4, 0],
      scale: [1, 1.08, 1],
      transition: { repeat: Infinity, duration: 1.5, ease: 'easeInOut' },
    },
  },
  failed: {
    animate: {
      y: [0, 4, 2],
      scale: [1, 0.95, 0.95],
      opacity: [1, 0.75, 1],
      transition: { repeat: Infinity, duration: 2, ease: 'easeInOut' },
    },
  },
};

export function TurtleAvatar({
  character,
  state = 'idle',
  size = 64,
  className = '',
}: TurtleAvatarProps) {
  const charId = resolveCharacterId(character);
  const variant = ANIMATION_VARIANTS[state] || ANIMATION_VARIANTS.idle;
  const dimension = typeof size === 'number' ? size : undefined;

  return (
    <motion.svg
      data-testid={`turtle-avatar-${charId}`}
      data-state={state}
      aria-label={`${charId} turtle avatar (${state})`}
      viewBox="0 0 100 100"
      width={dimension}
      height={dimension}
      className={`shrink-0 select-none overflow-visible ${className}`}
      {...variant}
    >
      <defs>
        {/* Speedy gradients */}
        <radialGradient id="speedy-shell" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#FBBF24" />
          <stop offset="100%" stopColor="#D97706" />
        </radialGradient>
        {/* Sheldon gradients */}
        <radialGradient id="sheldon-shell" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#FB7185" />
          <stop offset="100%" stopColor="#BE123C" />
        </radialGradient>
        {/* Sensei gradients */}
        <radialGradient id="sensei-shell" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#A78BFA" />
          <stop offset="100%" stopColor="#6D28D9" />
        </radialGradient>
        {/* Judge gradients */}
        <radialGradient id="judge-shell" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#38BDF8" />
          <stop offset="100%" stopColor="#0369A1" />
        </radialGradient>
      </defs>

      {/* Flippers / Feet */}
      <ellipse cx="26" cy="35" rx="9" ry="6" fill="#15803D" transform="rotate(-30 26 35)" />
      <ellipse cx="74" cy="35" rx="9" ry="6" fill="#15803D" transform="rotate(30 74 35)" />
      <ellipse cx="28" cy="72" rx="8" ry="5" fill="#166534" transform="rotate(35 28 72)" />
      <ellipse cx="72" cy="72" rx="8" ry="5" fill="#166534" transform="rotate(-35 72 72)" />
      {/* Little tail */}
      <polygon points="50,84 46,92 54,92" fill="#15803D" />

      {/* Head */}
      <circle cx="50" cy="24" r="14" fill="#22C55E" stroke="#15803D" strokeWidth="2" />

      {/* Eyes base */}
      <circle cx="44" cy="22" r="3" fill="#FFFFFF" />
      <circle cx="56" cy="22" r="3" fill="#FFFFFF" />
      <circle cx="45" cy="22" r="1.5" fill="#0F172A" />
      <circle cx="57" cy="22" r="1.5" fill="#0F172A" />

      {/* Turtle Shell Main */}
      {charId === 'speedy' && (
        <circle cx="50" cy="56" r="28" fill="url(#speedy-shell)" stroke="#B45309" strokeWidth="3" />
      )}
      {charId === 'sheldon' && (
        <circle cx="50" cy="56" r="28" fill="url(#sheldon-shell)" stroke="#9F1239" strokeWidth="3" />
      )}
      {charId === 'sensei' && (
        <circle cx="50" cy="56" r="28" fill="url(#sensei-shell)" stroke="#5B21B6" strokeWidth="3" />
      )}
      {charId === 'judge' && (
        <circle cx="50" cy="56" r="28" fill="url(#judge-shell)" stroke="#075985" strokeWidth="3" />
      )}

      {/* Shell Inner Hex Pattern */}
      <polygon points="50,42 63,49 63,63 50,70 37,63 37,49" fill="none" stroke="rgba(255,255,255,0.4)" strokeWidth="2" />

      {/* Character Specific Accessories */}
      {charId === 'speedy' && (
        <g id="speedy-accessories">
          {/* Goggles on head */}
          <rect x="38" y="18" width="24" height="6" rx="3" fill="#F97316" stroke="#C2410C" strokeWidth="1" />
          <circle cx="44" cy="21" r="3.5" fill="#FEF08A" stroke="#EA580C" strokeWidth="1" />
          <circle cx="56" cy="21" r="3.5" fill="#FEF08A" stroke="#EA580C" strokeWidth="1" />
          {/* Lightning bolt on shell */}
          <polygon points="52,48 45,57 49,57 47,66 56,55 51,55" fill="#FEF08A" stroke="#CA8A04" strokeWidth="1" />
        </g>
      )}

      {charId === 'sheldon' && (
        <g id="sheldon-accessories">
          {/* Dark sunglasses */}
          <polygon points="38,19 49,19 47,25 40,25" fill="#0F172A" />
          <polygon points="51,19 62,19 60,25 53,25" fill="#0F172A" />
          <line x1="47" y1="21" x2="53" y2="21" stroke="#0F172A" strokeWidth="1.5" />
          {/* Lock on shell */}
          <rect x="45" y="54" width="10" height="9" rx="2" fill="#FDE047" stroke="#854D0E" strokeWidth="1" />
          <path d="M47 54 V50 A3 3 0 0 1 53 50 V54" fill="none" stroke="#FDE047" strokeWidth="2" />
        </g>
      )}

      {charId === 'sensei' && (
        <g id="sensei-accessories">
          {/* White Headband with Red Sun */}
          <path d="M36 17 Q50 15 64 17" fill="none" stroke="#F8FAFC" strokeWidth="3" />
          <circle cx="50" cy="16" r="2" fill="#EF4444" />
          {/* Wise eyebrows */}
          <path d="M41 19 L46 20" stroke="#FFFFFF" strokeWidth="1.5" strokeLinecap="round" />
          <path d="M59 19 L54 20" stroke="#FFFFFF" strokeWidth="1.5" strokeLinecap="round" />
          {/* Master White Mustache */}
          <path d="M47 27 Q43 30 38 29" fill="none" stroke="#FFFFFF" strokeWidth="1.5" strokeLinecap="round" />
          <path d="M53 27 Q57 30 62 29" fill="none" stroke="#FFFFFF" strokeWidth="1.5" strokeLinecap="round" />
          {/* Zen Yin-Yang circle on shell */}
          <circle cx="50" cy="56" r="6" fill="#EDE9FE" />
          <path d="M50 50 A3 3 0 0 1 50 56 A3 3 0 0 0 50 62 A6 6 0 0 1 50 50" fill="#6D28D9" />
          <circle cx="50" cy="53" r="1" fill="#EDE9FE" />
          <circle cx="50" cy="59" r="1" fill="#6D28D9" />
        </g>
      )}

      {charId === 'judge' && (
        <g id="judge-accessories">
          {/* Judge Wig */}
          <path d="M37 17 C35 12 65 12 63 17 C67 21 65 29 63 31 C60 27 60 20 50 20 C40 20 40 27 37 31 C35 29 33 21 37 17 Z" fill="#E2E8F0" stroke="#94A3B8" strokeWidth="1" />
          {/* Scales of Justice emblem on shell */}
          <line x1="50" y1="48" x2="50" y2="64" stroke="#FDE047" strokeWidth="2" />
          <line x1="43" y1="51" x2="57" y2="51" stroke="#FDE047" strokeWidth="2" />
          <path d="M41 57 L45 57 L43 51 Z" fill="#FDE047" />
          <path d="M55 57 L59 57 L57 51 Z" fill="#FDE047" />
        </g>
      )}

      {/* State overlays */}
      {state === 'failed' && (
        <g>
          <line x1="42" y1="19" x2="46" y2="25" stroke="#EF4444" strokeWidth="2" />
          <line x1="46" y1="19" x2="42" y2="25" stroke="#EF4444" strokeWidth="2" />
          <line x1="54" y1="19" x2="58" y2="25" stroke="#EF4444" strokeWidth="2" />
          <line x1="58" y1="19" x2="54" y2="25" stroke="#EF4444" strokeWidth="2" />
        </g>
      )}
      {(state === 'thinking' || state === 'running') && (
        <circle cx="50" cy="56" r="28" fill="none" stroke="#60A5FA" strokeWidth="2" strokeDasharray="6 4" className="animate-spin origin-[50px_56px]" />
      )}
      {state === 'completed' && (
        <circle cx="50" cy="56" r="28" fill="none" stroke="#4ADE80" strokeWidth="1.5" strokeDasharray="3 3" />
      )}
    </motion.svg>
  );
}
