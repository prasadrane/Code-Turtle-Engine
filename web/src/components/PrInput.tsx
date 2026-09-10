import React, { useState } from 'react';
import { Sparkles, ArrowRight, Play, AlertCircle, GitPullRequest } from 'lucide-react';

export interface PrPreset {
  label: string;
  url: string;
  description?: string;
}

export const DEFAULT_PRESETS: PrPreset[] = [
  {
    label: 'dotnet/runtime #108214',
    url: 'https://github.com/dotnet/runtime/pull/108214',
    description: 'Span allocation optimization',
  },
  {
    label: 'dotnet/roslyn #73921',
    url: 'https://github.com/dotnet/roslyn/pull/73921',
    description: 'Compiler semantic model fix',
  },
  {
    label: 'dotnet/aspnetcore #56789',
    url: 'https://github.com/dotnet/aspnetcore/pull/56789',
    description: 'Kestrel HTTP/3 connection handler',
  },
];

export const GITHUB_PR_REGEX = /^https:\/\/github\.com\/[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+\/pull\/\d+(?:\/.*)?$/;

export interface PrInputProps {
  onSubmit: (url: string) => void;
  onTryDemo: () => void;
  isLoading?: boolean;
  disabled?: boolean;
  initialUrl?: string;
  presets?: PrPreset[];
  className?: string;
}

export function PrInput({
  onSubmit,
  onTryDemo,
  isLoading = false,
  disabled = false,
  initialUrl = '',
  presets = DEFAULT_PRESETS,
  className = '',
}: PrInputProps) {
  const [url, setUrl] = useState(initialUrl);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (disabled || isLoading) return;

    const trimmed = url.trim();
    if (!trimmed) {
      setErrorMessage('Please enter a valid GitHub PR URL (e.g., https://github.com/owner/repo/pull/123).');
      return;
    }

    if (!GITHUB_PR_REGEX.test(trimmed)) {
      setErrorMessage('Please enter a valid GitHub PR URL (e.g., https://github.com/owner/repo/pull/123).');
      return;
    }

    setErrorMessage(null);
    onSubmit(trimmed);
  };

  const handleSelectPreset = (presetUrl: string) => {
    setUrl(presetUrl);
    setErrorMessage(null);
  };

  return (
    <div className={`w-full max-w-3xl mx-auto flex flex-col gap-4 ${className}`} data-testid="pr-input-container">
      <form onSubmit={handleSubmit} className="relative flex flex-col sm:flex-row items-center gap-3">
        <div className="relative w-full flex-1">
          <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400">
            <GitPullRequest className="w-5 h-5 text-emerald-400" />
          </div>
          <input
            type="url"
            value={url}
            onChange={(e) => {
              setUrl(e.target.value);
              if (errorMessage) setErrorMessage(null);
            }}
            disabled={disabled || isLoading}
            placeholder="https://github.com/owner/repo/pull/123"
            aria-label="GitHub Pull Request URL"
            className={`w-full pl-11 pr-4 py-3.5 rounded-xl bg-slate-900/90 border text-slate-100 placeholder-slate-500 font-mono text-sm focus:outline-none focus:ring-2 transition-all shadow-inner ${
              errorMessage
                ? 'border-rose-500/80 focus:ring-rose-500/50'
                : 'border-slate-700/80 focus:border-emerald-500/80 focus:ring-emerald-500/30'
            } ${disabled || isLoading ? 'opacity-60 cursor-not-allowed' : ''}`}
          />
        </div>

        <div className="flex items-center gap-2 w-full sm:w-auto justify-end">
          <button
            type="submit"
            disabled={disabled || isLoading}
            className={`flex-1 sm:flex-initial inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-xl font-semibold text-sm transition-all shadow-lg ${
              disabled || isLoading
                ? 'bg-slate-800 text-slate-500 cursor-not-allowed'
                : 'bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-400 hover:to-teal-500 text-slate-950 font-bold hover:shadow-emerald-500/20 active:scale-[0.98]'
            }`}
          >
            {isLoading ? (
              <>
                <span className="inline-block w-4 h-4 border-2 border-slate-900 border-t-transparent rounded-full animate-spin" />
                <span>Reviewing...</span>
              </>
            ) : (
              <>
                <span>Start Review</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </div>
      </form>

      {errorMessage && (
        <div
          role="alert"
          className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-rose-950/50 border border-rose-800/60 text-rose-300 text-sm animate-fadeIn"
        >
          <AlertCircle className="w-4 h-4 flex-shrink-0 text-rose-400" />
          <span>{errorMessage}</span>
        </div>
      )}

      {/* Preset Chips and Try Demo Button */}
      <div className="flex flex-wrap items-center gap-2 pt-1 text-xs">
        <span className="text-slate-400 font-medium mr-1">Quick Select:</span>
        <button
          type="button"
          onClick={onTryDemo}
          disabled={disabled || isLoading}
          aria-label="Try Demo"
          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-500/10 hover:bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 font-semibold transition-all hover:scale-[1.02] active:scale-[0.98]"
        >
          <Sparkles className="w-3.5 h-3.5 text-emerald-400" />
          <span>Try Demo</span>
          <span className="ml-0.5 px-1.5 py-0.5 rounded text-[10px] bg-emerald-500/20 text-emerald-200">
            Instant
          </span>
        </button>

        {presets.map((preset) => (
          <button
            key={preset.url}
            type="button"
            onClick={() => handleSelectPreset(preset.url)}
            disabled={disabled || isLoading}
            title={preset.description || preset.url}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800/70 hover:bg-slate-700/80 border border-slate-700 text-slate-300 transition-colors font-mono text-[11px]"
          >
            <span>{preset.label}</span>
          </button>
        ))}
      </div>
    </div>
  );
}
