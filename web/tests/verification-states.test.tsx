import React from 'react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, renderHook, act } from '@testing-library/react';
import { FindingCard } from '@/components/FindingCard';
import { FinalReportView } from '@/components/FinalReportView';
import { TrustAuditVisualizer } from '@/components/TrustAuditVisualizer';
import { useReviewStream } from '@/hooks/useReviewStream';
import type { ReviewFinding, CouncilVerdict } from '@/lib/types';

const finding: ReviewFinding = { persona: 'SecurityAuditor', severity: 'Warning', title: 'Fabricated member',
  detail: 'Untrusted suggestion', location: 'A.cs:99', citedSymbolFqns: ['Demo.Fake'] };
const verdict: CouncilVerdict = { personas: [{ persona: finding.persona, findings: [finding] }], synthesized: [],
  guardAudit: [{ citedFqn: 'Demo.Fake', verified: false, evidence: 'Rejected' }] };

afterEach(() => vi.unstubAllGlobals());

it('never verifies provisional or legacy citations and labels uncited advisory findings', () => {
  const { rerender } = render(<FindingCard finding={finding} />);
  expect(screen.getByTestId('symbol-provenance-chip').getAttribute('data-verified')).toBe('false');
  expect(screen.getByText(/Provisional/)).toBeTruthy();
  rerender(<FindingCard finding={finding} guardAudit={[{ citedFqn: 'Demo.Fake', verified: true }]} />);
  expect(screen.getByTestId('symbol-provenance-chip').getAttribute('data-verified')).toBe('false');
  rerender(<FindingCard finding={{ ...finding, citedSymbolFqns: [] }} />);
  expect(screen.getByText(/Advisory finding without citations/)).toBeTruthy();
});

it('shows all-removed and degraded states; rejected raw output stays visibly distinct', () => {
  render(<FinalReportView verdict={verdict} stats={{ personasSucceeded: 1 }} />);
  expect(screen.getByText(/All raw findings were removed/)).toBeTruthy();
  expect(screen.getByText(/No retained findings/)).toBeTruthy();
  expect(screen.queryByText(/Council Approved|exceptionally clean/)).toBeNull();
  fireEvent.click(screen.getByRole('tab', { name: /Persona Breakdown/ }));
  expect(screen.getByText(/not retained after arbitration/)).toBeTruthy();
  expect(screen.getByTestId('symbol-provenance-chip').className).toContain('line-through');
});

it('does not treat absent or legacy audit evidence as a perfect score', () => {
  const { rerender } = render(<TrustAuditVisualizer />);
  expect(screen.getByText('0%')).toBeTruthy();
  rerender(<TrustAuditVisualizer guardAudit={[{ citedFqn: 'Demo.Fake', verified: true }]} />);
  expect(screen.queryByTestId('status-verified')).toBeNull();
  expect(screen.getByText('Legacy / evidence unavailable')).toBeTruthy();
});

it('reconciles streamed raw findings with final stripped results, including partial citations', async () => {
  class Events {
    static latest: Events;
    listeners: Record<string, (event: { data: string }) => void> = {};
    onerror: (() => void) | null = null;
    constructor() { Events.latest = this; }
    addEventListener(type: string, fn: (event: { data: string }) => void) { this.listeners[type] = fn; }
    close() {}
    emit(type: string, data: unknown) { this.listeners[type]({ data: JSON.stringify(data) }); }
  }
  vi.stubGlobal('EventSource', Events);
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ jobId: 'id' }) }));
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  act(() => Events.latest.emit('persona_finding', { finding }));
  expect(result.current.findings).toEqual([finding]);
  const partial = { ...finding, title: 'Mixed', citedSymbolFqns: ['Demo.Real'] };
  const final = { ...verdict, synthesized: [partial] };
  act(() => Events.latest.emit('completed', { markdown: 'Done', verdict: final }));
  expect(result.current.findings).toEqual([partial]);
  expect(result.current.guardAudit).toEqual(verdict.guardAudit);
  expect(result.current.isCompleted).toBe(true);
});

it('failed reviews do not become completed reports', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('compilation failed')));
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.isCompleted).toBe(false);
  expect(result.current.verdict).toBeNull();
  expect(result.current.error).toBe('compilation failed');
});
