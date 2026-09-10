import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, ArrowRight, ShieldAlert, Sparkles } from 'lucide-react';

export interface LanguageRejectionModalProps {
  isOpen: boolean;
  type?: 'not_dotnet' | 'private_repo' | 'unknown_error' | string;
  detectedLanguage?: string;
  customMessage?: string;
  onClose: () => void;
  onRetry?: () => void;
}

const LANGUAGE_MESSAGES: Record<string, string> = {
  javascript: 'JavaScript? Our turtles tried to parse your semicolons... wait, there are none. C# only for now!',
  typescript: 'TypeScript has great types, but our turtles only speak compiled Roslyn IL! C# only for now.',
  python: 'Indentation-based languages make our turtles dizzy. We only speak curly braces — C# curly braces.',
  rust: 'Your borrow checker is impressive, but our turtles haven\'t learned ownership yet. C# only for now!',
  go: '`if err != nil` — we felt that. But our turtles only review C# for now!',
  java: 'So close! Same family, different shell. C# only for now!',
};

export function LanguageRejectionModal({
  isOpen,
  type = 'not_dotnet',
  detectedLanguage = 'other',
  customMessage,
  onClose,
  onRetry,
}: LanguageRejectionModalProps) {
  if (!isOpen) return null;

  const normalizedType = (type || '').toLowerCase();
  const isPrivateRepo = normalizedType === 'private_repo' || normalizedType.includes('private');
  const langKey = (detectedLanguage || '').toLowerCase();
  const funnyMessage =
    customMessage ||
    (isPrivateRepo
      ? 'Halt! This repository appears to be private — or this PR doesn\'t exist. The Turtle Council only reviews public code. No peeking behind private curtains!'
      : LANGUAGE_MESSAGES[langKey] ||
        'Interesting language! Our turtles are still in C# school. More languages coming soon!');

  return (
    <AnimatePresence>
      <div
        data-testid={isPrivateRepo ? 'private-repo-modal' : 'language-rejection-modal'}
        className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm"
        role="dialog"
        aria-modal="true"
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.9, y: 12 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.9, y: 12 }}
          transition={{ type: 'spring', damping: 25, stiffness: 350 }}
          className="relative w-full max-w-lg rounded-2xl bg-slate-900 border border-slate-700/80 p-6 shadow-2xl overflow-hidden"
        >
          {/* Close icon button */}
          <button
            type="button"
            onClick={onClose}
            className="absolute top-4 right-4 p-1.5 rounded-lg text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition-colors"
            aria-label="Close"
          >
            <X className="w-5 h-5" />
          </button>

          {/* Humorous Graphics Display */}
          <div className="flex flex-col items-center text-center mt-2">
            {isPrivateRepo ? (
              /* Security Turtle with Sunglasses & Velvet Rope */
              <div data-testid="security-turtle" className="relative w-40 h-32 my-2 flex items-center justify-center">
                {/* Velvet Rope */}
                <div className="absolute inset-x-0 bottom-4 flex items-center justify-between px-2 z-20">
                  <div className="w-3 h-14 bg-amber-400 rounded-t-sm border border-amber-500 shadow-md" />
                  <motion.div
                    initial={{ scaleX: 0.8 }}
                    animate={{ scaleX: 1 }}
                    transition={{ repeat: Infinity, duration: 2, repeatType: 'reverse' }}
                    className="flex-1 h-3 bg-red-600 rounded-full shadow-lg mx-1 border border-red-700 relative top-2"
                  />
                  <div className="w-3 h-14 bg-amber-400 rounded-t-sm border border-amber-500 shadow-md" />
                </div>

                {/* Turtle with Dark Sunglasses */}
                <motion.svg
                  viewBox="0 0 100 100"
                  className="w-24 h-24 relative z-10 filter drop-shadow-lg"
                  initial={{ y: 6 }}
                  animate={{ y: [6, 2, 6] }}
                  transition={{ repeat: Infinity, duration: 2.5, ease: 'easeInOut' }}
                >
                  {/* Turtle Shell */}
                  <ellipse cx="50" cy="56" rx="32" ry="24" fill="#047857" stroke="#065f46" strokeWidth="2.5" />
                  <path d="M36 56 Q50 44 64 56 Q50 68 36 56" fill="none" stroke="#10b981" strokeWidth="2" />
                  {/* Head */}
                  <circle cx="50" cy="34" r="14" fill="#10b981" stroke="#047857" strokeWidth="2" />
                  {/* Security Sunglasses */}
                  <polygon points="40,31 48,31 47,37 41,37" fill="#0f172a" />
                  <polygon points="52,31 60,31 59,37 53,37" fill="#0f172a" />
                  <line x1="48" y1="33" x2="52" y2="33" stroke="#0f172a" strokeWidth="2" />
                  {/* Badge */}
                  <circle cx="50" cy="58" r="4" fill="#f59e0b" />
                </motion.svg>
              </div>
            ) : (
              /* Non-.NET Retreating Turtle Animation */
              <div data-testid="retreating-turtle" className="relative w-40 h-32 my-2 flex items-center justify-center">
                <motion.svg
                  viewBox="0 0 100 100"
                  className="w-24 h-24 filter drop-shadow-lg"
                  animate={{
                    x: [0, -10, -2, -14, 0],
                    rotate: [0, -4, 2, -5, 0],
                  }}
                  transition={{ repeat: Infinity, duration: 3.5, ease: 'easeInOut' }}
                >
                  {/* Turtle Shell */}
                  <ellipse cx="52" cy="54" rx="34" ry="26" fill="#15803d" stroke="#166534" strokeWidth="2.5" />
                  {/* Hexagon pattern on shell */}
                  <polygon points="52,42 62,47 62,59 52,64 42,59 42,47" fill="#22c55e" opacity="0.8" />
                  {/* Retreating Head (hides into shell on wobble) */}
                  <motion.circle
                    cx="74"
                    cy="48"
                    r="10"
                    fill="#4ade80"
                    stroke="#15803d"
                    strokeWidth="2"
                    animate={{ cx: [74, 62, 74] }}
                    transition={{ repeat: Infinity, duration: 2, ease: 'easeInOut' }}
                  />
                  {/* Sad or surprised eye */}
                  <motion.circle
                    cx="76"
                    cy="46"
                    r="2"
                    fill="#0f172a"
                    animate={{ opacity: [1, 0.2, 1] }}
                    transition={{ repeat: Infinity, duration: 1.5 }}
                  />
                  {/* Feet retreating */}
                  <ellipse cx="32" cy="70" rx="8" ry="5" fill="#22c55e" />
                  <ellipse cx="64" cy="70" rx="8" ry="5" fill="#22c55e" />
                </motion.svg>
              </div>
            )}

            {/* Badge */}
            <div className="mt-2 mb-3">
              {isPrivateRepo ? (
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30">
                  <ShieldAlert className="w-3.5 h-3.5" />
                  Access Restricted • Velvet Rope Up
                </span>
              ) : (
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                  <Sparkles className="w-3.5 h-3.5" />
                  {detectedLanguage.toUpperCase()} • Coming Soon
                </span>
              )}
            </div>

            {/* Title */}
            <h3 className="text-xl font-bold text-slate-100">
              {isPrivateRepo
                ? 'VIP Only — Velvet Rope Secured!'
                : `Whoa! ${detectedLanguage.charAt(0).toUpperCase() + detectedLanguage.slice(1)} Detected`}
            </h3>

            {/* Message */}
            <p className="text-sm text-slate-300 mt-2.5 leading-relaxed max-w-md">
              {funnyMessage}
            </p>

            {/* Action Buttons */}
            <div className="flex items-center justify-center gap-3 mt-6 w-full">
              <button
                type="button"
                data-testid="modal-close-btn"
                onClick={onClose}
                className="flex-1 px-4 py-2.5 rounded-xl bg-slate-800 text-slate-200 hover:bg-slate-700 text-xs font-semibold transition-colors border border-slate-700"
              >
                Close
              </button>
              <button
                type="button"
                onClick={() => {
                  if (onRetry) onRetry();
                  else onClose();
                }}
                className="flex-1 inline-flex items-center justify-center gap-1.5 px-4 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-all shadow-lg shadow-emerald-500/20"
              >
                <span>{isPrivateRepo ? 'Try a Public PR' : 'Try with a .NET PR'}</span>
                <ArrowRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
