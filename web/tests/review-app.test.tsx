import React, { StrictMode } from 'react';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReviewApp } from '@/components/ReviewApp';
import { initialReviewState } from '@/lib/reviewState';
import { workspaceReplay } from '@/data/workspace-demo';
import type { UseReviewStreamReturn } from '@/hooks/useReviewStream';
import type { ReviewFinding } from '@/lib/types';

const navigation = vi.hoisted(() => ({ query: '', push: vi.fn(), replace: vi.fn() }));
const liveMock = vi.hoisted(() => ({ current: null as unknown }));
vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(navigation.query),
  usePathname: () => '/review',
  useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
}));
vi.mock('@/hooks/useReviewStream', () => ({ useReviewStream: () => liveMock.current }));
// Probe the controller's replay result without coupling lifecycle tests to panel markup.
// useDemoReplay itself is deliberately real, including its timer cleanup effect.
vi.mock('@/components/ReviewWorkspace', () => ({
  ReviewWorkspace: ({ status, findings, controls }: {
    status: string; findings: ReviewFinding[]; controls?: React.ReactNode;
  }) => <section data-testid="review-probe" data-status={status}>
    <output data-testid="retained-findings">{JSON.stringify(findings.map(finding => finding.title))}</output>
    {controls}
  </section>,
}));

let live: UseReviewStreamReturn;
const findings = () => JSON.parse(screen.getByTestId('retained-findings').textContent || '[]') as string[];
const status = () => screen.getByTestId('review-probe').getAttribute('data-status');
// Replay timers do no asynchronous work. Flush them synchronously so parallel suite
// load cannot turn fake-time advancement into a wall-clock timeout or leave act open.
const advance = (ms: number) => { act(() => { vi.advanceTimersByTime(ms); }); };
const newReview = () => screen.getByRole('button', { name: /^new review$/i });
const homeLink = () => screen.getByRole('link', { name: /Code Turtle.*home|home.*Code Turtle/i });
const prField = () => screen.getByRole('textbox', { name: 'GitHub Pull Request URL' });

function startLive(status: 'submitting' | 'queued' | 'running' | 'completed' = 'running') {
  live.status = status;
  live.isStreaming = status !== 'completed';
  live.isCompleted = status === 'completed';
  live.hasStarted = status !== 'submitting';
  fireEvent.change(prField(), { target: { value: 'https://github.com/fixture/repo/pull/1' } });
  fireEvent.click(screen.getByRole('button', { name: /start review/i }));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.clearAllMocks();
  navigation.query = '';
  navigation.replace.mockImplementation((url: string) => { navigation.query = url.split('?')[1] || ''; });
  live = {
    ...initialReviewState, apiBase: 'http://localhost:5000', connectionError: null,
    isStreaming: false, isCompleted: false, currentPhase: '', personaStates: initialReviewState.personas,
    guardAudit: [], verdict: null, markdown: null,
    startReview: vi.fn().mockResolvedValue(undefined), cancel: vi.fn(), reset: vi.fn(), clearRejection: vi.fn(),
  };
  liveMock.current = live;
  window.scrollTo = vi.fn();
  Element.prototype.scrollIntoView = vi.fn();
});

afterEach(() => {
  try { cleanup(); }
  finally {
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  }
});

describe('ReviewApp sample URL lifecycle', () => {
  it('advances the real replay through completion under StrictMode without duplicate findings', async () => {
    navigation.query = 'sample=1';
    render(<StrictMode><ReviewApp /></StrictMode>);
    await advance(2300);
    expect(findings()).toEqual(['Avoid materializing a temporary list']);
    await advance(1000);
    expect(findings()).toEqual(['Avoid materializing a temporary list', 'Parameterize the customer query']);
    await advance(2000);
    const finalEvent = workspaceReplay.find(item => item.event.type === 'completed')!.event;
    if (finalEvent.type !== 'completed') throw new Error('Fixture has no completed event');
    expect(status()).toBe('completed');
    expect(findings()).toEqual(finalEvent.verdict.synthesized.map(finding => finding.title));
    expect(new Set(findings()).size).toBe(findings().length);
    expect(live.startReview).not.toHaveBeenCalled();
  });

  it('does not restart on rerender and starts fresh when the sample URL is re-entered', async () => {
    navigation.query = 'sample=1';
    const view = render(<ReviewApp />);
    await advance(2300);
    view.rerender(<ReviewApp />);
    await advance(1000);
    expect(findings()).toHaveLength(2);
    await advance(2000);
    expect(status()).toBe('completed');
    navigation.query = '';
    view.rerender(<ReviewApp />);
    expect(prField()).toBeTruthy();
    navigation.query = 'sample=1';
    view.rerender(<ReviewApp />);
    expect(status()).toBe('running');
    expect(findings()).toEqual([]);
    await advance(5100);
    expect(status()).toBe('completed');
    expect(findings()).toHaveLength(2);
  });

  it.each([true, false])('reset clears sample intent and focuses an available launcher target (configured: %s)', async configured => {
    if (!configured) {
      live.apiBase = null;
      live.connectionError = 'Live reviews are unavailable here. Set up Code Turtle locally.';
    }
    navigation.query = 'sample=1';
    render(<ReviewApp />);
    fireEvent.click(newReview());
    await advance(20);
    expect(navigation.replace).toHaveBeenCalledWith('/review');
    expect(screen.queryByRole('dialog')).toBeNull();
    if (configured) expect(document.activeElement).toBe(prField());
    else {
      expect((prField() as HTMLInputElement).disabled).toBe(true);
      const focused = document.activeElement;
      expect(focused?.tagName.match(/^H[1-6]$/) || focused?.textContent?.match(/try sample review/i)).toBeTruthy();
    }
  });

  it('keeps replay controls in the status bar and expands mobile progress without restarting playback', async () => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
    navigation.query = 'sample=1';
    render(<ReviewApp />);
    const footer = screen.getByRole('contentinfo', { name: 'Review status and playback' });
    const toggle = within(footer).getByRole('button', { name: /reviewers/i });
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    fireEvent.click(toggle);
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(document.getElementById(toggle.getAttribute('aria-controls')!)?.getAttribute('data-expanded')).toBe('true');
    expect(within(screen.getByRole('banner')).queryByRole('button', { name: /pause/i })).toBeNull();
    await advance(2300);
    fireEvent.click(within(footer).getByRole('button', { name: /^pause$/i }));
    await advance(4000);
    expect(findings()).toHaveLength(1);
    fireEvent.click(within(footer).getByRole('button', { name: /^resume$/i }));
    fireEvent.click(within(footer).getByRole('button', { name: '5x' }));
    expect(within(footer).getByRole('button', { name: '5x' }).getAttribute('aria-pressed')).toBe('true');
    await advance(5100);
    expect(status()).toBe('completed');
    fireEvent.click(within(footer).getByRole('button', { name: /replay sample/i }));
    expect(navigation.push).toHaveBeenCalledWith('/review?sample=1');
    expect(findings()).toEqual([]);
  });
});

describe('ReviewApp live leave protection', () => {
  it('traps dialog keyboard focus and restores the initiating control when Escape keeps the review', () => {
    render(<ReviewApp />);
    startLive();
    newReview().focus();
    fireEvent.click(newReview());
    const dialog = screen.getByRole('dialog');
    const stay = within(dialog).getByRole('button', { name: /stay on review/i });
    const leave = within(dialog).getByRole('button', { name: /stop review and leave/i });
    expect(document.activeElement).toBe(stay);
    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(document.activeElement).toBe(leave);
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(document.activeElement).toBe(stay);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(newReview());
    expect(status()).toBe('running');
  });
  it.each(['submitting', 'queued', 'running'] as const)('guards New review while live status is %s; staying preserves the review', async activeStatus => {
    render(<ReviewApp />);
    startLive(activeStatus);
    const resetCalls = vi.mocked(live.reset).mock.calls.length;
    fireEvent.click(newReview());
    const dialog = screen.getByRole('dialog');
    expect(dialog.textContent).toMatch(/stop|stopped/i);
    fireEvent.click(within(dialog).getByRole('button', { name: /stay on review/i }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(live.reset).toHaveBeenCalledTimes(resetCalls);
    expect(live.cancel).not.toHaveBeenCalled();
    expect(status()).toBe(activeStatus);
    fireEvent.click(newReview());
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /stop review and leave/i }));
    await advance(20);
    expect(prField()).toBeTruthy();
    expect(live.reset).toHaveBeenCalledTimes(resetCalls + 1);
  });

  it.each(['submitting', 'queued', 'running'] as const)('guards the home link while live status is %s', activeStatus => {
    render(<ReviewApp />);
    startLive(activeStatus);
    fireEvent.click(homeLink());
    expect(navigation.push).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /stay on review/i }));
    expect(status()).toBe(activeStatus);
    fireEvent.click(homeLink());
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /stop review and leave/i }));
    expect(navigation.push).toHaveBeenCalledWith('/');
  });

  it('registers the best-effort unload warning only during active live work and removes it on unmount', () => {
    const view = render(<ReviewApp />);
    const idle = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(idle);
    expect(idle.defaultPrevented).toBe(false);
    startLive();
    const active = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(active);
    expect(active.defaultPrevented).toBe(true);
    live.isStreaming = false;
    live.status = 'completed';
    live.isCompleted = true;
    view.rerender(<ReviewApp />);
    const completed = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(completed);
    expect(completed.defaultPrevented).toBe(false);
    live.isStreaming = true;
    view.rerender(<ReviewApp />);
    view.unmount();
    const unmounted = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(unmounted);
    expect(unmounted.defaultPrevented).toBe(false);
  });

  it('does not confirm completed live work or sample replay', () => {
    const view = render(<ReviewApp />);
    startLive('completed');
    fireEvent.click(homeLink());
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(navigation.push).toHaveBeenCalledWith('/');
    view.unmount();
    navigation.query = 'sample=1';
    render(<ReviewApp />);
    const sample = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(sample);
    expect(sample.defaultPrevented).toBe(false);
    fireEvent.click(homeLink());
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});
