import React, { type ReactNode } from 'react';
import '../workspace.css';

export default function ReviewLayout({ children }: { children: ReactNode }) {
  return <div className="app-shell">
    <a href="#review-main" className="skip-link">Skip to review workspace</a>
    {children}
  </div>;
}
