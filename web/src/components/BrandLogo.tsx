import React from 'react';

export function TurtleMark({ className = '' }: { className?: string; }) {
  return (
    <svg viewBox="0 0 64 64" fill="none" aria-hidden="true" className={className}>
      <path d="m20 18-5-7m5 35-5 7m25-35 5-7m-5 35 5 7" stroke="currentColor" strokeWidth="8" strokeLinecap="round" />
      <circle cx="54" cy="32" r="7" fill="currentColor" />
      <path d="m10 29-5 3 5 3" fill="currentColor" />
      <rect x="9" y="14" width="40" height="36" rx="18" fill="currentColor" />
      <path d="m23 26-6 6 6 6m12-12 6 6-6 6m-4-15-4 18" stroke="var(--logo-detail, #f7f9f3)" strokeWidth="2.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function BrandLogo({ compact = false }: { compact?: boolean; }) {
  return <span className="brand-logo">
    <TurtleMark className="brand-mark" />{!compact && <span>code<span className="brand-word-light">turtle</span>
      <span className="brand-period">.</span>
    </span>}</span>;
}
