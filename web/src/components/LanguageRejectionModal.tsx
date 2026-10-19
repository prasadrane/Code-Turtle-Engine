import React, { useEffect, useId, useRef } from 'react';
import { X, ArrowUpRight } from 'lucide-react';
import { TurtleMark } from './BrandLogo';

export interface LanguageRejectionModalProps { isOpen: boolean; type?: string; detectedLanguage?: string; customMessage?: string; onClose: () => void; onRetry?: () => void; }
export function LanguageRejectionModal({ isOpen, type, detectedLanguage, customMessage, onClose, onRetry }: LanguageRejectionModalProps) {
  const ref = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  useEffect(() => {
    if (!isOpen) return;
    const previous = document.activeElement as HTMLElement | null;
    ref.current?.querySelector<HTMLButtonElement>('button')?.focus();
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
      if (event.key !== 'Tab') return;
      const targets = Array.from(ref.current?.querySelectorAll<HTMLButtonElement>('button') ?? []);
      const first = targets[0], last = targets[targets.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('keydown', escape); previous?.focus(); };
  }, [isOpen, onClose]);
  if (!isOpen) return null;
  const inaccessible = type === 'private_repo' || type?.includes('private');
  return <div className="review-modal-backdrop"><div ref={ref} className="review-modal" role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId}
    data-testid={inaccessible ? 'private-repo-modal' : 'language-rejection-modal'}>
    <button type="button" className="modal-close" aria-label="Close" onClick={onClose}><X size={19} /></button>
    <span className="entry-icon"><TurtleMark /></span><h2 id={titleId}>{inaccessible ? 'Pull request unavailable' : 'C# reviews are supported today'}</h2>
    <p id={descriptionId}>{customMessage || (inaccessible ? 'We could not access this pull request. Check that it exists, is public, and is allowed by your local server.'
      : `${detectedLanguage ? `${detectedLanguage.charAt(0).toUpperCase() + detectedLanguage.slice(1)} was detected. ` : ''}Code Turtle currently reviews C# source using Roslyn compiler facts. Choose a supported C# pull request or explore the sample.`)}</p>
    <div className="modal-actions"><button type="button" data-testid="modal-close-btn" onClick={onClose}>Back to review</button>
      {onRetry && <button type="button" className="button button-green" onClick={onRetry}>Explore sample review <ArrowUpRight size={15} /></button>}</div>
  </div></div>;
}
