import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useDemoReplay } from '@/hooks/useDemoReplay';
import type { ReplayEventItem } from '@/lib/types';

describe('useDemoReplay Hook', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const sampleEvents: ReplayEventItem[] = [
    { delay: 100, event: { type: 'phase', phase: 'validating', message: 'Validating...' } },
    { delay: 300, event: { type: 'persona_started', persona: 'AllocationsPerformance', character: 'Speedy' } },
    {
      delay: 500,
      event: {
        type: 'persona_finding',
        persona: 'AllocationsPerformance',
        character: 'Speedy',
        finding: {
          persona: 'AllocationsPerformance',
          severity: 'Warning',
          title: 'Heap Allocation',
          detail: 'Boxing detected',
          location: 'File.cs:10',
          citedSymbolFqns: ['System.Int32'],
        },
      },
    },
    {
      delay: 700,
      event: {
        type: 'persona_completed',
        persona: 'AllocationsPerformance',
        character: 'Speedy',
        findingCount: 1,
        quip: '1 allocation found!',
      },
    },
    {
      delay: 900,
      event: {
        type: 'guard_audit',
        character: 'Judge Shellsworth',
        verified: 1,
        stripped: 0,
        total: 1,
        auditDetails: [{ citedFqn: 'System.Int32', verified: true }],
      },
    },
    {
      delay: 1100,
      event: {
        type: 'completed',
        markdown: '# Verdict\nDone.',
        verdict: {
          personas: [],
          synthesized: [],
          guardAudit: [{ citedFqn: 'System.Int32', verified: true }],
        },
      },
    },
  ];

  it('initializes with default idle state', () => {
    const { result } = renderHook(() => useDemoReplay({ events: sampleEvents }));
    expect(result.current.isPlaying).toBe(false);
    expect(result.current.isPaused).toBe(false);
    expect(result.current.isCompleted).toBe(false);
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.progress).toBe(0);
    expect(result.current.findings).toHaveLength(0);
  });

  it('plays through events and updates aggregated state as time progresses', () => {
    const { result } = renderHook(() => useDemoReplay({ events: sampleEvents }));

    act(() => {
      result.current.start();
    });

    expect(result.current.isPlaying).toBe(true);

    // Advance past phase event (delay: 100)
    act(() => {
      vi.advanceTimersByTime(150);
    });
    expect(result.current.currentPhase).toBe('validating');
    expect(result.current.phaseMessage).toBe('Validating...');

    // Advance past persona_started (delay: 300)
    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(result.current.personaStates['AllocationsPerformance']?.status).toBe('running');

    // Advance past finding and completed (delay: 500, 700)
    act(() => {
      vi.advanceTimersByTime(400);
    });
    expect(result.current.findings).toHaveLength(1);
    expect(result.current.findings[0].title).toBe('Heap Allocation');
    expect(result.current.personaStates['AllocationsPerformance']?.status).toBe('completed');

    // Advance to end (delay: 1100)
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(result.current.isCompleted).toBe(true);
    expect(result.current.isPlaying).toBe(false);
    expect(result.current.completedMarkdown).toBe('# Verdict\nDone.');
    expect(result.current.guardAudit).toHaveLength(1);
    expect(result.current.progress).toBe(1);
  });

  it('supports pause and resume', () => {
    const { result } = renderHook(() => useDemoReplay({ events: sampleEvents }));

    act(() => {
      result.current.start();
    });

    act(() => {
      vi.advanceTimersByTime(150);
    });
    expect(result.current.currentPhase).toBe('validating');

    act(() => {
      result.current.pause();
    });
    expect(result.current.isPaused).toBe(true);
    expect(result.current.isPlaying).toBe(false);

    // Time passes while paused; no events should trigger
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(result.current.findings).toHaveLength(0);

    act(() => {
      result.current.resume();
    });
    expect(result.current.isPlaying).toBe(true);
    expect(result.current.isPaused).toBe(false);

    // Advance to trigger remaining events
    act(() => {
      vi.advanceTimersByTime(1100);
    });
    expect(result.current.isCompleted).toBe(true);
    expect(result.current.findings).toHaveLength(1);
  });

  it('supports reset back to initial state', () => {
    const { result } = renderHook(() => useDemoReplay({ events: sampleEvents }));

    act(() => {
      result.current.start();
      vi.advanceTimersByTime(600);
    });
    expect(result.current.findings).toHaveLength(1);

    act(() => {
      result.current.reset();
    });
    expect(result.current.isPlaying).toBe(false);
    expect(result.current.isCompleted).toBe(false);
    expect(result.current.findings).toHaveLength(0);
    expect(result.current.currentIndex).toBe(0);
  });

  it('supports skipToEnd', () => {
    const { result } = renderHook(() => useDemoReplay({ events: sampleEvents }));

    act(() => {
      result.current.skipToEnd();
    });
    expect(result.current.isCompleted).toBe(true);
    expect(result.current.findings).toHaveLength(1);
    expect(result.current.completedMarkdown).toBe('# Verdict\nDone.');
    expect(result.current.guardAudit).toHaveLength(1);
  });
});
