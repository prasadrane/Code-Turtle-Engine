import React, { useId, useState } from 'react';
import { ArrowRight, Play, AlertCircle, GitPullRequest } from 'lucide-react';

export interface PrPreset { label: string; url: string; description?: string; }

// Illustrative URLs to demonstrate input format; availability is not guaranteed.
export const DEFAULT_PRESETS: PrPreset[] = [
  { label: 'dotnet/runtime #108214', url: 'https://github.com/dotnet/runtime/pull/108214', description: 'Example URL; availability is not guaranteed' },
  { label: 'dotnet/roslyn #73921', url: 'https://github.com/dotnet/roslyn/pull/73921', description: 'Example URL; availability is not guaranteed' },
  { label: 'dotnet/aspnetcore #56789', url: 'https://github.com/dotnet/aspnetcore/pull/56789', description: 'Example URL; availability is not guaranteed' },
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
  showDemo?: boolean;
}

export function PrInput({ onSubmit, onTryDemo, isLoading = false, disabled = false, initialUrl = '', presets = DEFAULT_PRESETS, className = '', showDemo = true }: PrInputProps) {
  const [url, setUrl] = useState(initialUrl);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const id = useId();
  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (disabled || isLoading) return;
    const trimmed = url.trim();
    if (!GITHUB_PR_REGEX.test(trimmed)) {
      setErrorMessage('Please enter a valid GitHub PR URL (e.g., https://github.com/owner/repo/pull/123).');
      return;
    }
    setErrorMessage(null);
    onSubmit(trimmed);
  };

  return <div className={`pr-input ${className}`} data-testid="pr-input-container">
    <form onSubmit={handleSubmit} noValidate>
      <label className="input-label" htmlFor={id}>Pull request URL</label>
      <div className="pr-input-row">
        <div className="pr-url-field">
          <GitPullRequest size={19} />
          <input id={id} type="url" value={url} onChange={event => { setUrl(event.target.value); setErrorMessage(null); }} disabled={disabled || isLoading} placeholder="https://github.com/owner/repo/pull/123" aria-label="GitHub Pull Request URL" aria-invalid={Boolean(errorMessage)} aria-describedby={errorMessage ? `${id}-error` : undefined} />
        </div>
        <button type="submit" disabled={disabled || isLoading} className="button button-green">{isLoading ? 'Reviewing...' : 'Start Review'}<ArrowRight size={17} />
        </button>
      </div>
    </form>
    {errorMessage && <div id={`${id}-error`} role="alert" className="input-error">
      <AlertCircle size={16} />
      <span>{errorMessage}</span>
    </div>}
    <div className="preset-row">
      <span>Example URLs</span>{showDemo && <button type="button" onClick={onTryDemo} disabled={disabled || isLoading} className="preset-demo" aria-label="Try Demo">
        <Play size={12} /> Try Demo</button>}{presets.map(preset => <button key={preset.url} type="button" onClick={() => { setUrl(preset.url); setErrorMessage(null); }} disabled={disabled || isLoading} title={preset.description || preset.url}>{preset.label}</button>)}</div>
  </div>;
}
