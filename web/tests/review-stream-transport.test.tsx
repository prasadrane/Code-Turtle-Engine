import { afterEach, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useReviewStream } from '@/hooks/useReviewStream';
import type { ReviewChanges } from '@/lib/changes';
import type { CouncilVerdict, ReviewFinding } from '@/lib/types';

const finding: ReviewFinding = { persona: 'SecurityAuditor', severity: 'Warning', title: 'Inspect input',
  detail: 'Check the query parameters.', location: 'Untrusted.cs:99', citedSymbolFqns: ['Demo.Service.Read'] };
const verdict: CouncilVerdict = { personas: [], synthesized: [finding], guardAudit: [] };
const artifact: ReviewChanges = { schemaVersion: 1,
  snapshot: { repository: 'demo/repo', pullNumber: 1, title: 'Update service', baseSha: 'a'.repeat(40),
    headSha: 'b'.repeat(40), mergeBaseSha: 'a'.repeat(40), comparisonKind: 'merge-base-to-head' },
  files: [], anchors: [] };

class Stream {
  static instances: Stream[] = [];
  listeners: Record<string, (event: { data: string }) => void> = {};
  onerror: (() => void) | null = null;
  close = vi.fn();
  constructor() { Stream.instances.push(this); }
  addEventListener(type: string, handler: (event: { data: string }) => void) { this.listeners[type] = handler; }
  emit(type: string, data: unknown) { this.listeners[type]?.({ data: JSON.stringify(data) }); }
}
const response = (data: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => data });
function setupTransport(artifactResponse = Promise.resolve(response(artifact))) {
  Stream.instances = [];
  vi.stubGlobal('EventSource', Stream);
  const transport = vi.fn().mockImplementation((url: string, options?: RequestInit) => {
    if (url.endsWith('/changes')) return artifactResponse;
    if (options?.method === 'DELETE') return Promise.resolve(response(null, 204));
    return Promise.resolve(response({ jobId: `job-${Stream.instances.length}` }));
  });
  vi.stubGlobal('fetch', transport);
  return transport;
}
afterEach(() => vi.unstubAllGlobals());

it('aborts the request, closes the stream, and attempts authenticated job cancellation on unmount', async () => {
  const transport = setupTransport();
  const { result, unmount } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  const stream = Stream.instances[0];
  const [, request] = transport.mock.calls.find(([, options]) => options?.method === 'POST')!;
  const signal = request.signal as AbortSignal;
  expect(signal.aborted).toBe(false);
  unmount();
  expect(signal.aborted).toBe(true);
  expect(stream.close).toHaveBeenCalledOnce();
  expect(transport).toHaveBeenCalledWith(expect.stringContaining('/api/review/job-0'),
    expect.objectContaining({ method: 'DELETE', credentials: 'include' }));
});

it('cancels a job returned after unmount without creating a stale event stream', async () => {
  Stream.instances = [];
  vi.stubGlobal('EventSource', Stream);
  let resolve!: (value: ReturnType<typeof response>) => void;
  const pending = new Promise<ReturnType<typeof response>>(done => { resolve = done; });
  const transport = vi.fn().mockImplementation((_: string, options: RequestInit) =>
    options.method === 'DELETE' ? Promise.resolve(response(null, 204)) : pending);
  vi.stubGlobal('fetch', transport);
  const { result, unmount } = renderHook(() => useReviewStream());
  let starting!: Promise<void>;
  act(() => { starting = result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('submitting');
  unmount();
  await act(async () => { resolve(response({ jobId: 'late-job' })); await starting; });
  expect(Stream.instances).toHaveLength(0);
  expect(transport).toHaveBeenCalledWith(expect.stringContaining('/api/review/late-job'),
    expect.objectContaining({ method: 'DELETE', credentials: 'include' }));
});

it('loads the pinned browser artifact after completion and ignores late stream events', async () => {
  const transport = setupTransport();
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  const stream = Stream.instances[0];
  await act(async () => { stream.emit('completed', { markdown: 'Review', verdict }); });
  await waitFor(() => expect(result.current.changes).toEqual(artifact));
  expect(transport).toHaveBeenCalledWith(expect.stringContaining('/job-0/changes'), expect.objectContaining({
    credentials: 'include', cache: 'no-store', signal: expect.any(AbortSignal) }));
  expect(stream.close).toHaveBeenCalledOnce();
  act(() => {
    stream.emit('persona_finding', { finding: { ...finding, title: 'Late finding' } });
    stream.onerror?.();
  });
  expect(result.current.findings).toEqual([finding]);
  expect(result.current.status).toBe('completed');
  expect(result.current.error).toBeNull();
});

it('retains the completed report when its source artifact is unavailable', async () => {
  setupTransport(Promise.resolve(response({}, 202)));
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  await act(async () => { Stream.instances[0].emit('completed', { markdown: 'Review', verdict }); });
  await waitFor(() => expect(result.current.changesError).toContain('not available'));
  expect(result.current.status).toBe('completed');
  expect(result.current.verdict).toEqual(verdict);
  expect(result.current.changes).toBeNull();
});

it('does not attach an old artifact after returning to setup', async () => {
  let resolve!: (value: ReturnType<typeof response>) => void;
  setupTransport(new Promise(done => { resolve = done; }));
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  act(() => Stream.instances[0].emit('completed', { markdown: 'Review', verdict }));
  act(() => result.current.reset());
  await act(async () => { resolve(response(artifact)); });
  expect(result.current.status).toBe('idle');
  expect(result.current.changes).toBeNull();
  expect(result.current.changesError).toBeNull();
});

it('marks transport failures as interrupted without converting partial notes into a report', async () => {
  setupTransport();
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  const stream = Stream.instances[0];
  act(() => { stream.emit('persona_finding', { finding }); stream.onerror?.(); });
  expect(result.current.status).toBe('failed');
  expect(result.current.hasStarted).toBe(true);
  expect(result.current.findings).toEqual([finding]);
  expect(result.current.verdict).toBeNull();
  expect(result.current.error).toContain('incomplete');
  expect(stream.close).toHaveBeenCalledOnce();
});

it('keeps terminal server errors and ignores events from a previous review', async () => {
  const transport = setupTransport();
  const { result } = renderHook(() => useReviewStream());
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  const previous = Stream.instances[0];
  act(() => previous.emit('error', { code: 'COMPILATION_FAILED', message: 'Could not load compilation.' }));
  act(() => previous.onerror?.());
  expect(result.current.error).toEqual({ code: 'COMPILATION_FAILED', message: 'Could not load compilation.' });
  expect(transport.mock.calls.some(([url]) => url.endsWith('/changes'))).toBe(false);
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/2'); });
  act(() => previous.emit('completed', { markdown: 'Old review', verdict }));
  expect(result.current.status).toBe('queued');
  expect(result.current.verdict).toBeNull();
  expect(result.current.findings).toEqual([]);
});
