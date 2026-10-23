import React from 'react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act, renderHook, waitFor, within } from '@testing-library/react';
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

it('preserves overview filters, report tab, selected file and source mode for one review', async () => {
  const other = { ...changes.files[0], path: 'Other.cs', oldPath: 'Other.cs', source: 'class Other {}' };
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict}
    changes={{ ...changes, files: [...changes.files, other] }} audit={[]} personas={{}} phase="completed" message="Complete" markdown="Report" />);
  fireEvent.change(screen.getByRole('textbox', { name: 'Search findings' }), { target: { value: 'query' } });
  fireEvent.click(screen.getByTestId('filter-severity-critical'));
  fireEvent.click(screen.getByTestId('filter-persona-SecurityAuditor'));
  const origin = screen.getByRole('button', { name: 'View code context' });
  fireEvent.click(origin);
  await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('inline-comment-0')));
  fireEvent.click(screen.getByRole('button', { name: 'Head source' }));
  fireEvent.change(screen.getByLabelText('Select changed file'), { target: { value: 'Other.cs' } });
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  expect(document.activeElement).toBe(origin);
  expect((screen.getByRole('textbox', { name: 'Search findings' }) as HTMLInputElement).value).toBe('query');
  expect(screen.getByTestId('filter-severity-critical').getAttribute('aria-pressed')).toBe('true');
  expect(screen.getByTestId('filter-persona-SecurityAuditor').getAttribute('aria-pressed')).toBe('true');
  fireEvent.click(screen.getByRole('tab', { name: 'Markdown Review' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  expect((screen.getByLabelText('Select changed file') as HTMLSelectElement).value).toBe('Other.cs');
  expect(screen.getByRole('button', { name: 'Head source' }).getAttribute('aria-pressed')).toBe('true');
  expect(screen.getByRole('region', { name: 'Other.cs head source' }).textContent).toContain('class Other {}');
  fireEvent.click(screen.getByRole('tab', { name: 'Review overview' }));
  expect(screen.getByRole('tab', { name: 'Markdown Review' }).getAttribute('aria-selected')).toBe('true');
});

it('restores the originating report panel and action after a manual Markdown detour', async () => {
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict} changes={changes}
    audit={[]} personas={{}} phase="completed" message="Complete" markdown="Report" />);
  fireEvent.change(screen.getByRole('textbox', { name: 'Search findings' }), { target: { value: 'query' } });
  fireEvent.click(screen.getByTestId('filter-severity-critical'));
  const overview = screen.getByTestId('final-report-view').closest('.workspace-view') as HTMLElement;
  overview.scrollTop = 150;
  fireEvent.scroll(overview);
  const origin = screen.getByRole('button', { name: 'View code context' });
  fireEvent.click(origin);
  fireEvent.click(screen.getByRole('button', { name: 'Head source' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Review overview' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Markdown Review' }));
  overview.scrollTop = 0;
  fireEvent.scroll(overview);
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  await waitFor(() => expect(screen.getByRole('tab', { name: 'Synthesized Verdict' }).getAttribute('aria-selected')).toBe('true'));
  expect(document.activeElement).toBe(origin);
  expect(origin.isConnected).toBe(true);
  expect(overview.scrollTop).toBe(150);
  expect((screen.getByRole('textbox', { name: 'Search findings' }) as HTMLInputElement).value).toBe('query');
  expect(screen.getByTestId('filter-severity-critical').getAttribute('aria-pressed')).toBe('true');
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  expect(screen.getByRole('button', { name: 'Head source' }).getAttribute('aria-pressed')).toBe('true');
});

it('lists all retained findings in the inspector and returns to the actual inspector action', async () => {
  const unanchored = { ...finding, title: 'Unanchored recommendation', severity: 'Info' as const };
  const elsewhere = { ...finding, title: 'Other member recommendation' };
  const retained = [finding, unanchored, elsewhere];
  const multiFile = { ...changes, files: [...changes.files, { ...changes.files[0], path: 'Other.cs', oldPath: 'Other.cs' }],
    anchors: [...changes.anchors, { ...changes.anchors[0], findingIndex: 2, path: 'Other.cs' }] };
  render(<ReviewWorkspace status="completed" isDemo={false} findings={retained} verdict={{ ...verdict, synthesized: retained }}
    changes={multiFile} audit={[]} personas={{}} phase="completed" message="Complete" />);
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  const inspector = screen.getByRole('complementary', { name: 'Findings inspector' });
  expect(within(inspector).getAllByTestId('finding-card')).toHaveLength(3);
  expect(within(inspector).getByText('Unanchored recommendation')).toBeTruthy();
  expect(within(inspector).getByText(/No code anchor/)).toBeTruthy();
  const origin = within(inspector).getByRole('button', { name: 'View code context: Other member recommendation' });
  fireEvent.click(origin);
  await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('inline-comment-2')));
  expect((screen.getByLabelText('Select changed file') as HTMLSelectElement).value).toBe('Other.cs');
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  expect(screen.getByRole('tab', { name: 'Changes' }).getAttribute('aria-selected')).toBe('true');
  expect(document.activeElement).toBe(origin);
  expect(origin.isConnected).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Hide findings' }));
  expect(screen.queryByRole('complementary', { name: 'Findings inspector' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  expect(screen.getByRole('complementary', { name: 'Findings inspector' })).toBe(inspector);
  expect(document.activeElement).toBe(origin);
  expect(screen.getByRole('tab', { name: 'Changes' }).getAttribute('aria-selected')).toBe('true');
});

it('keeps focus in the visible Changes toolbar when an inspector origin becomes mobile-only hidden', () => {
  let width = 1280;
  const listeners = new Set<() => void>();
  vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
    get matches() { return width >= (query.includes('1200') ? 1200 : 768); },
    addEventListener: (_event: string, callback: () => void) => listeners.add(callback),
    removeEventListener: (_event: string, callback: () => void) => listeners.delete(callback),
  })));
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict} changes={changes}
    audit={[]} personas={{}} phase="completed" message="Complete" />);
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  const inspector = screen.getByRole('complementary', { name: 'Findings inspector' });
  fireEvent.click(within(inspector).getByRole('button', { name: `View code context: ${finding.title}` }));
  act(() => { width = 375; listeners.forEach(listener => listener()); });
  const back = screen.getByRole('button', { name: 'Back to finding' });
  fireEvent.click(back);
  expect(document.activeElement).toBe(back);
  expect(screen.getByRole('tab', { name: 'Changes' }).getAttribute('aria-selected')).toBe('true');
  expect(screen.queryByRole('complementary', { name: 'Findings inspector' })).toBeNull();
});

it('restores pane and document scroll positions without replacing the originating control', () => {
  vi.stubGlobal('scrollY', 0);
  const scrollTo = vi.fn((position: ScrollToOptions) => { vi.stubGlobal('scrollY', position.top ?? 0); });
  vi.stubGlobal('scrollTo', scrollTo);
  render(<ReviewWorkspace status="completed" isDemo={false} findings={[finding]} verdict={verdict} changes={changes}
    audit={[]} personas={{}} phase="completed" message="Complete" />);
  const overview = screen.getByTestId('final-report-view').closest('.workspace-view') as HTMLElement;
  overview.scrollTop = 145;
  fireEvent.scroll(overview);
  vi.stubGlobal('scrollY', 320);
  const origin = screen.getByRole('button', { name: 'View code context' });
  fireEvent.click(origin);
  const codePane = screen.getByTestId('code-context-viewer').querySelector('.code-main') as HTMLElement;
  codePane.scrollTop = 210;
  fireEvent.scroll(codePane);
  const inspector = screen.getByRole('complementary', { name: 'Findings inspector' });
  inspector.scrollTop = 75;
  fireEvent.scroll(inspector);
  fireEvent.click(screen.getByRole('button', { name: 'Back to finding' }));
  expect(overview.scrollTop).toBe(145);
  expect(document.activeElement).toBe(origin);
  expect(scrollTo).toHaveBeenLastCalledWith({ top: 320, behavior: 'auto' });
  // Simulate a browser clearing hidden overflow offsets; activation restores saved positions.
  codePane.scrollTop = 0;
  inspector.scrollTop = 0;
  fireEvent.click(screen.getByRole('tab', { name: 'Changes' }));
  expect(codePane.scrollTop).toBe(210);
  expect(inspector.scrollTop).toBe(75);
});

it('leaves the chosen workspace view active when the final verdict arrives', () => {
  const props = { isDemo: false, findings: [finding], changes, audit: [], personas: {}, phase: 'running', message: 'Reviewing' };
  const { rerender } = render(<ReviewWorkspace {...props} status="running" />);
  fireEvent.click(screen.getByRole('tab', { name: 'Evidence' }));
  rerender(<ReviewWorkspace {...props} status="completed" verdict={verdict} />);
  expect(screen.getByRole('tab', { name: 'Evidence' }).getAttribute('aria-selected')).toBe('true');
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
