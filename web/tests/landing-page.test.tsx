import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import React from 'react';
import { PrInput, DEFAULT_PRESETS } from '@/components/PrInput';
import { HeroHeader } from '@/components/HeroHeader';
import { useReviewStream } from '@/hooks/useReviewStream';
import HomePage from '@/app/page';

describe('PrInput Component', () => {
  beforeEach(() => {
    window.scrollTo = vi.fn();
  });
  it('renders input, presets, try demo chip, and start review button', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} />);

    expect(screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i)).toBeTruthy();
    expect(screen.getByRole('button', { name: /start review/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /try demo/i })).toBeTruthy();
    expect(screen.getByText('dotnet/runtime #108214')).toBeTruthy();
  });

  it('validates URL and calls onSubmit when valid PR URL is submitted', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} />);

    const input = screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i);
    const submitBtn = screen.getByRole('button', { name: /start review/i });

    fireEvent.change(input, { target: { value: 'https://github.com/dotnet/runtime/pull/999' } });
    fireEvent.click(submitBtn);

    expect(onSubmit).toHaveBeenCalledWith('https://github.com/dotnet/runtime/pull/999');
    expect(screen.queryByText(/valid github pr url/i)).toBeNull();
  });

  it('shows validation error when invalid URL is submitted', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} />);

    const input = screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i);
    const submitBtn = screen.getByRole('button', { name: /start review/i });

    fireEvent.change(input, { target: { value: 'https://gitlab.com/owner/repo/merge_requests/1' } });
    fireEvent.click(submitBtn);

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/valid github pr url/i)).toBeTruthy();
  });

  it('triggers onTryDemo when Try Demo button/chip is clicked', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} />);

    const demoBtn = screen.getByRole('button', { name: /try demo/i });
    fireEvent.click(demoBtn);

    expect(onTryDemo).toHaveBeenCalledTimes(1);
  });

  it('populates input when preset chip is clicked', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} />);

    const presetChip = screen.getByText('dotnet/runtime #108214');
    fireEvent.click(presetChip);

    const input = screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i) as HTMLInputElement;
    expect(input.value).toBe(DEFAULT_PRESETS[0].url);
  });

  it('disables input and submit button when loading', () => {
    const onSubmit = vi.fn();
    const onTryDemo = vi.fn();

    render(<PrInput onSubmit={onSubmit} onTryDemo={onTryDemo} isLoading={true} />);

    const input = screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i) as HTMLInputElement;
    const submitBtn = screen.getByRole('button', { name: /reviewing/i }) as HTMLButtonElement;

    expect(input.disabled).toBe(true);
    expect(submitBtn.disabled).toBe(true);
  });
});

describe('HeroHeader Component', () => {
  it('renders heading, description, and feature pills', () => {
    render(<HeroHeader />);

    expect(screen.getAllByText(/Code-Turtle/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/AI Review Council/i)).toBeTruthy();
    expect(screen.getByText(/Roslyn Citation Guard/i)).toBeTruthy();
  });
});

describe('useReviewStream Hook', () => {
  class MockEventSource {
    url: string;
    listeners: Record<string, ((event: MessageEvent) => void)[]> = {};
    close = vi.fn();

    constructor(url: string) {
      this.url = url;
    }

    addEventListener(type: string, listener: (event: MessageEvent) => void) {
      if (!this.listeners[type]) this.listeners[type] = [];
      this.listeners[type].push(listener);
    }

    emit(type: string, data: any) {
      const listeners = this.listeners[type] || [];
      const event = { data: JSON.stringify(data) } as MessageEvent;
      listeners.forEach((fn) => fn(event));
    }
  }

  let originalFetch: typeof global.fetch;
  let originalEventSource: any;
  let activeMockEs: MockEventSource | null = null;

  beforeEach(() => {
    originalFetch = global.fetch;
    originalEventSource = (global as any).EventSource;
    (global as any).EventSource = vi.fn((url: string) => {
      activeMockEs = new MockEventSource(url);
      return activeMockEs;
    });
  });

  afterEach(() => {
    global.fetch = originalFetch;
    (global as any).EventSource = originalEventSource;
    activeMockEs = null;
  });

  it('handles successful review lifecycle via EventSource', async () => {
    const { renderHook, act } = await import('@testing-library/react');

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 202,
      json: async () => ({ jobId: 'job-123', streamUrl: '/api/review/job-123/stream' }),
    });

    const { result } = renderHook(() => useReviewStream({ apiBase: 'http://localhost:5000' }));

    expect(result.current.isStreaming).toBe(false);

    await act(async () => {
      await result.current.startReview('https://github.com/dotnet/runtime/pull/100');
    });

    expect(result.current.isStreaming).toBe(true);
    expect(activeMockEs).not.toBeNull();
    expect(activeMockEs?.url).toBe('http://localhost:5000/api/review/job-123/stream');

    act(() => {
      activeMockEs?.emit('phase', { phase: 'validating', message: 'Validating PR...' });
    });
    expect(result.current.phase).toBe('validating');
    expect(result.current.phaseMessage).toBe('Validating PR...');

    act(() => {
      activeMockEs?.emit('persona_started', { persona: 'AllocationsPerformance', character: 'Speedy' });
    });
    expect(result.current.personas.AllocationsPerformance.status).toBe('running');

    act(() => {
      activeMockEs?.emit('guard_audit', {
        character: 'Judge Shellsworth',
        verified: 1,
        stripped: 0,
        total: 1,
        auditDetails: [{ citedFqn: 'System.Span<T>', verified: true }],
      });
    });
    expect(result.current.auditSymbols.length).toBe(1);

    act(() => {
      activeMockEs?.emit('completed', {
        markdown: '# Review Report',
        verdict: { personas: [], synthesized: [], guardAudit: [] },
      });
    });
    expect(result.current.isStreaming).toBe(false);
    expect(result.current.isCompleted).toBe(true);
    expect(result.current.finalVerdict).not.toBeNull();
  });

  it('handles non-dotnet rejection on API start review', async () => {
    const { renderHook, act } = await import('@testing-library/react');

    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        code: 'NOT_DOTNET',
        language: 'python',
        error: 'Python code detected! C# only.',
      }),
    });

    const { result } = renderHook(() => useReviewStream());

    await act(async () => {
      await result.current.startReview('https://github.com/owner/python-repo/pull/1');
    });

    expect(result.current.isStreaming).toBe(false);
    expect(result.current.rejection?.isOpen).toBe(true);
    expect(result.current.rejection?.type).toBe('not_dotnet');
  });
});

describe('HomePage Integration', () => {
  it('renders idle landing page with PrInput and HeroHeader', () => {
    render(<HomePage />);

    expect(screen.getByText(/AI Review Council/i)).toBeTruthy();
    expect(screen.getByPlaceholderText(/github\.com\/owner\/repo\/pull\/123/i)).toBeTruthy();
    expect(screen.getByRole('button', { name: /try demo/i })).toBeTruthy();
  });

  it('transitions to demo mode when Try Demo is clicked', async () => {
    render(<HomePage />);

    const demoBtn = screen.getByRole('button', { name: /try demo/i });
    fireEvent.click(demoBtn);

    await waitFor(() => {
      expect(screen.getByTestId('phase-timeline')).toBeTruthy();
      expect(screen.getByTestId('council-chamber')).toBeTruthy();
    });
  });
});
