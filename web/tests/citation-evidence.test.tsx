import React from 'react';
import { it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TrustAuditVisualizer } from '@/components/TrustAuditVisualizer';

it('distinguishes declaration signatures from name-only and unrelated evidence', () => {
  render(<TrustAuditVisualizer guardAudit={[
    { citedFqn: 'Demo.A.Run(int)', verified: true, evidence: 'Compilation',
      resolution: { exists: true, match: 'Signature', symbols: [
        { assembly: 'Demo, Version=0.0.0.0', documentationId: 'M:Demo.A.Run(System.Int32)', display: 'Demo.A.Run(int)' },
      ] }, payloadContexts: [{ file: 'Changed.cs', member: 'Check', startLine: 2, endLine: 5 }] },
    { citedFqn: 'Demo.A.Other', verified: true, evidence: 'Compilation',
      resolution: { exists: true, match: 'NameOnly', symbols: [] }, payloadContexts: [] },
  ]} />);
  expect(screen.getByText('Declaration signature matched')).toBeTruthy();
  expect(screen.getByText('Symbol-name existence only')).toBeTruthy();
  expect(screen.getByText('M:Demo.A.Run(System.Int32)')).toBeTruthy();
  expect(screen.getByText(/Changed.cs · Check/)).toBeTruthy();
  expect(screen.getByText(/No matching name in extracted member facts/)).toBeTruthy();
  expect(screen.queryByText('Recommendation verified')).toBeNull();
});
