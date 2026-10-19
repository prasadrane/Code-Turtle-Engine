import React from 'react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act, renderHook, waitFor } from '@testing-library/react';
import { ReviewWorkspace } from '@/components/ReviewWorkspace';
import { CodeContextViewer } from '@/components/CodeContextViewer';
import { FinalReportView } from '@/components/FinalReportView';
import { useReviewStream } from '@/hooks/useReviewStream';
import type { ReviewChanges } from '@/lib/changes';
import type { CouncilVerdict, ReviewFinding } from '@/lib/types';

const finding: ReviewFinding = { persona: 'SecurityAuditor', severity: 'Critical', title: 'Inspect query input',
  detail: 'Use parameters for user input.', location: 'Wrong.cs:999', citedSymbolFqns: ['Demo.Service.Read'] };
const verdict: CouncilVerdict = { personas: [], synthesized: [finding], guardAudit: [] };
const changes: ReviewChanges = { schemaVersion: 1, snapshot: { repository: 'demo/repo', pullNumber: 7,
  title: 'Update service', baseSha: 'a'.repeat(40), headSha: 'b'.repeat(40), mergeBaseSha: 'a'.repeat(40),
  comparisonKind: 'merge-base-to-head' }, files: [{ path: 'Service.cs', oldPath: 'Service.cs', status: 'Modified',
  analyzed: true, source: 'class Service\n{\n    int Read() => 2;\n}', hunks: [{ header: '@@ -1,4 +1,4 @@', lines: [
    { kind: 'context', oldLine: 1, newLine: 1, text: 'class Service' },
    { kind: 'context', oldLine: 2, newLine: 2, text: '{' },
    { kind: 'removed', oldLine: 3, newLine: null, text: '    int Read() => 1;' },
    { kind: 'added', oldLine: null, newLine: 3, text: '    int Read() => 2;' },
  ] }] }], anchors: [{ findingIndex: 0, path: 'Service.cs', startLine: 3, endLine: 3, provenance: 'RoslynMember' }] };

afterEach(() => vi.unstubAllGlobals());

it('renders an inline comment only at a supplied deterministic anchor, not a model location', () => {
  render(<CodeContextViewer changes={changes} findings={[finding]} />);
  expect(screen.getByRole('heading', { name: finding.title })).toBeTruthy();
  expect(screen.getByText(/Member context/)).toBeTruthy();
  expect(screen.queryByText('Wrong.cs:999')).toBeNull();
  expect(screen.getByTestId('inline-comment-0').getAttribute('data-line')).toBe('3');
});

it('keeps unanchored findings out of code and provides full head-source navigation', () => {
  render(<CodeContextViewer changes={{ ...changes, anchors: [] }} findings={[finding]} />);
  expect(screen.queryByTestId('inline-comment-0')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Head source' }));
  expect(screen.getByRole('region', { name: 'Service.cs head source' }).textContent).toContain('int Read() => 2;');
});

it('uses findings as the primary view when source artifacts are unavailable', () => {
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict}
    audit={[]} personas={{}} phase="completed" message="Complete" markdown="Report" />);
  expect(screen.getByText(finding.title)).toBeTruthy();
  expect(screen.getByText(/Source and diff unavailable/)).toBeTruthy();
  expect(screen.queryByText(/GetOrderAsync/)).toBeNull();
});

it('supports keyboard tabs and exposes selected severity state', () => {
  render(<FinalReportView verdict={verdict} markdown="Report" />);
  const tab = screen.getByRole('tab', { name: /Synthesized Verdict/ });
  tab.focus();
  fireEvent.keyDown(tab, { key: 'ArrowRight' });
  expect(screen.getByRole('tab', { name: /Persona Breakdown/ }).getAttribute('aria-selected')).toBe('true');
  fireEvent.keyDown(document.activeElement!, { key: 'Home' });
  const critical = screen.getByTestId('filter-severity-critical');
  fireEvent.click(critical);
  expect(critical.getAttribute('aria-pressed')).toBe('true');
  expect(screen.getByRole('textbox', { name: 'Search findings' })).toBeTruthy();
});

it('does not attach final-array anchors to provisional findings with a different order', () => {
  render(<ReviewWorkspace status="running" isDemo={true} findings={[{ ...finding, title: 'Provisional performance note' }]} changes={changes}
    audit={[]} personas={{}} phase="deliberating" message="Reviewing" />);
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  expect(screen.getByTestId('code-context-viewer')).toBeTruthy();
  expect(screen.queryByTestId('inline-comment-0')).toBeNull();
});

it('restores the originating finding action after inspecting code', async () => {
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict} changes={changes}
    audit={[]} personas={{}} phase="completed" message="Complete" />);
  fireEvent.click(screen.getByRole('button', { name: 'View code context' }));
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('button', { name: 'View code context' })));
});

it('includes historical sample provenance when copying Markdown', async () => {
  const writeText = vi.fn().mockResolvedValue(undefined);
  Object.assign(navigator, { clipboard: { writeText } });
  render(<FinalReportView verdict={verdict} markdown="6/7 citations verified against Roslyn AST."
    reportOrigin="historical" />);
  fireEvent.click(screen.getByRole('tab', { name: /Markdown Review/ }));
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: /Copy Markdown/ })); });
  expect(writeText).toHaveBeenCalledWith(expect.stringContaining('Historical recorded sample'));
  expect(writeText).toHaveBeenCalledWith(expect.stringContaining('unavailable'));
});

it('distinguishes failed starts, cancellation, and stream failures', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Failed to fetch')));
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(result.current.hasStarted).toBe(false);
  act(() => result.current.cancel());
  expect(result.current.status).toBe('cancelled');
});

it('ignores late start responses after cancellation', async () => {
  let resolve!: (value: unknown) => void;
  vi.stubGlobal('fetch', vi.fn().mockImplementation(() => new Promise(r => { resolve = r; })));
  const eventSource = vi.fn();
  vi.stubGlobal('EventSource', eventSource);
  const { result } = renderHook(() => useReviewStream());
  let starting!: Promise<void>;
  act(() => { starting = result.current.startReview('https://github.com/o/r/pull/1'); });
  act(() => result.current.cancel());
  await act(async () => { resolve({ ok: true, json: async () => ({ jobId: 'late' }) }); await starting; });
  await waitFor(() => expect(result.current.status).toBe('cancelled'));
  expect(eventSource).not.toHaveBeenCalled();
});
