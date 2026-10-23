'use client';

import React, { useEffect, useId, useRef } from 'react';

export function LeaveReviewDialog({ onStay, onLeave }: { onStay: () => void; onLeave: () => void }) {
  const root = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    root.current?.querySelector<HTMLButtonElement>('button')?.focus();
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); onStay(); }
      if (event.key !== 'Tab') return;
      const targets = Array.from(root.current?.querySelectorAll<HTMLButtonElement>('button') ?? []);
      const first = targets[0], last = targets[targets.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', handleKey);
    return () => { document.removeEventListener('keydown', handleKey); previous?.focus(); };
  }, [onStay]);
  return <div className="review-modal-backdrop"><div ref={root} className="review-modal" role="dialog"
    aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId}>
    <h2 id={titleId}>Stop this review?</h2>
    <p id={descriptionId}>Leaving or starting a new review will stop this live review. Stay here to let it finish.</p>
    <div className="modal-actions">
      <button type="button" onClick={onStay}>Stay on review</button>
      <button type="button" onClick={onLeave}>Stop review and leave</button>
    </div>
  </div></div>;
}
