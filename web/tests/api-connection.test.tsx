import { afterEach, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import React from 'react';
import { renderToString } from 'react-dom/server';
import { hydrateRoot } from 'react-dom/client';
import { useReviewStream } from '@/hooks/useReviewStream';
import * as apiConnection from '@/lib/apiConnection';

function mockTransport() {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ jobId: 'configured-job' }) });
  const streamMock = vi.fn().mockImplementation(() => ({ addEventListener() {}, close() {} }));
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('EventSource', streamMock);
  return { fetchMock, streamMock };
}
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs(); vi.restoreAllMocks(); });

it.each([undefined, '', '   '])('blocks production live reviews without a configured API (%s)', async configured => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.stubEnv('NEXT_PUBLIC_API_BASE', configured);
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ accessToken: 'do-not-send' }));
  expect(result.current.connectionError).toMatch(/live review server/i);
  expect(result.current.apiBase).toBeNull();
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(result.current.error).toBe(result.current.connectionError);
  expect(result.current.hasStarted).toBe(false);
  expect(fetchMock).not.toHaveBeenCalled();
  expect(streamMock).not.toHaveBeenCalled();
});

it.each(['development', 'test'])('preserves the local API default in %s', async environment => {
  vi.stubEnv('NODE_ENV', environment);
  vi.stubEnv('NEXT_PUBLIC_API_BASE', '  ');
  const { fetchMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream());
  expect(result.current.connectionError).toBeNull();
  expect(result.current.apiBase).toBe('http://localhost:5000');
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(fetchMock).toHaveBeenCalledWith('http://localhost:5000/api/review', expect.any(Object));
});

it('uses an explicitly configured API in production and keeps credentials out of stream URLs', async () => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.stubEnv('NEXT_PUBLIC_API_BASE', '  https://api.example.test///  ');
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ accessToken: 'local-access-key' }));
  expect(result.current.connectionError).toBeNull();
  expect(result.current.apiBase).toBe('https://api.example.test');
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(fetchMock).toHaveBeenCalledWith('https://api.example.test/api/session', expect.objectContaining({
    credentials: 'include', headers: { Authorization: 'Bearer local-access-key' } }));
  expect(streamMock).toHaveBeenCalledWith('https://api.example.test/api/review/configured-job/stream', { withCredentials: true });
});

it.each(['/api', 'not-a-url', 'ftp://api.example.test', 'javascript:alert(1)',
  'https://user:secret@api.example.test', 'https://api.example.test?key=secret', 'https://api.example.test#secret'])
('rejects invalid API configuration before sending credentials (%s)', async apiBase => {
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase, accessToken: 'do-not-send' }));
  expect(result.current.connectionError).toMatch(/http/i);
  expect(result.current.connectionError).not.toContain('secret');
  expect(result.current.apiBase).toBeNull();
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(fetchMock).not.toHaveBeenCalled();
  expect(streamMock).not.toHaveBeenCalled();
});

it('replaces browser network TypeErrors with local-server recovery guidance', async () => {
  const { fetchMock, streamMock } = mockTransport();
  fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
  const { result } = renderHook(() => useReviewStream({ apiBase: 'http://localhost:5000' }));
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(result.current.error).toMatch(/cannot reach the review server/i);
  expect(result.current.error).toMatch(/same hostname/i);
  expect(result.current.error).not.toBe('Failed to fetch');
  expect(streamMock).not.toHaveBeenCalled();
});

it.each(['https://code-turtle.example', 'http://127.0.0.1:3100', 'https://localhost:3100'])
('blocks loopback API credentials from an incompatible frontend origin (%s)', async frontendOrigin => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue(frontendOrigin);
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase: 'http://localhost:5000', accessToken: 'do-not-send' }));
  expect(result.current.connectionError).toMatch(/same local hostname and scheme/i);
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(fetchMock).not.toHaveBeenCalled();
  expect(streamMock).not.toHaveBeenCalled();
});

it('allows an explicit local production API on the same hostname and scheme', async () => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue('http://localhost:3100');
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase: 'http://localhost:5000' }));
  expect(result.current.connectionError).toBeNull();
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(fetchMock).toHaveBeenCalledWith('http://localhost:5000/api/review', expect.any(Object));
  expect(streamMock).toHaveBeenCalledOnce();
});

it('blocks an HTTP API from an HTTPS frontend before sending credentials', async () => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue('https://code-turtle.example');
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase: 'http://api.example.test', accessToken: 'do-not-send' }));
  expect(result.current.connectionError).toMatch(/HTTPS/i);
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.status).toBe('failed');
  expect(fetchMock).not.toHaveBeenCalled();
  expect(streamMock).not.toHaveBeenCalled();
});

it('keeps an HTTPS API available from an HTTPS frontend', async () => {
  vi.stubEnv('NODE_ENV', 'production');
  vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue('https://code-turtle.example');
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase: 'https://api.example.test' }));
  expect(result.current.connectionError).toBeNull();
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(fetchMock).toHaveBeenCalledWith('https://api.example.test/api/review', expect.any(Object));
  expect(streamMock).toHaveBeenCalledOnce();
});

it('checks the current frontend origin again before sending a request', async () => {
  const origin = vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue('http://localhost:3100');
  const { fetchMock, streamMock } = mockTransport();
  const { result } = renderHook(() => useReviewStream({ apiBase: 'http://localhost:5000', accessToken: 'do-not-send' }));
  expect(result.current.connectionError).toBeNull();
  origin.mockReturnValue('https://code-turtle.example');
  await act(async () => { await result.current.startReview('https://github.com/o/r/pull/1'); });
  expect(result.current.error).toMatch(/same local hostname and scheme/i);
  expect(fetchMock).not.toHaveBeenCalled();
  expect(streamMock).not.toHaveBeenCalled();
});

it('hydrates the initial server output before applying client-origin guidance', async () => {
  vi.stubEnv('NODE_ENV', 'production');
  const origin = vi.spyOn(apiConnection, 'browserFrontendOrigin').mockReturnValue('https://code-turtle.example');
  const { fetchMock } = mockTransport();
  function Probe() {
    const review = useReviewStream({ apiBase: 'http://localhost:5000' });
    return React.createElement('p', null, review.connectionError || 'Ready');
  }
  const container = document.createElement('div');
  container.innerHTML = renderToString(React.createElement(Probe));
  expect(container.textContent).toBe('Ready');
  expect(origin).not.toHaveBeenCalled();
  document.body.append(container);
  const onRecoverableError = vi.fn();
  let root!: ReturnType<typeof hydrateRoot>;
  await act(async () => { root = hydrateRoot(container, React.createElement(Probe), { onRecoverableError }); });
  expect(container.textContent).toMatch(/same local hostname and scheme/i);
  expect(onRecoverableError).not.toHaveBeenCalled();
  expect(fetchMock).not.toHaveBeenCalled();
  act(() => root.unmount());
  container.remove();
});
