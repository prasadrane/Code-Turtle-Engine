import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { TurtleAvatar } from '@/components/TurtleAvatar';
import { FindingBadge } from '@/components/FindingBadge';
import { PersonaCard } from '@/components/PersonaCard';
import { CouncilChamber } from '@/components/CouncilChamber';
import { TURTLE_CHARACTERS } from '@/lib/characters';
import type { ReviewFinding } from '@/lib/types';
import type { PersonaState } from '@/hooks/useDemoReplay';

describe('FindingBadge Component', () => {
  it('renders severity text and appropriate styling for each severity level', () => {
    const { rerender } = render(<FindingBadge severity="Critical" />);
    expect(screen.getByText('Critical')).toBeTruthy();
    expect(screen.getByTestId('finding-badge').className).toContain('rose');

    rerender(<FindingBadge severity="Error" />);
    expect(screen.getByText('Error')).toBeTruthy();
    expect(screen.getByTestId('finding-badge').className).toContain('red');

    rerender(<FindingBadge severity="Warning" />);
    expect(screen.getByText('Warning')).toBeTruthy();
    expect(screen.getByTestId('finding-badge').className).toContain('amber');

    rerender(<FindingBadge severity="Nit" />);
    expect(screen.getByText('Nit')).toBeTruthy();
    expect(screen.getByTestId('finding-badge').className).toContain('sky');

    rerender(<FindingBadge severity="Info" />);
    expect(screen.getByText('Info')).toBeTruthy();
    expect(screen.getByTestId('finding-badge').className).toContain('slate');
  });

  it('renders count pill when count is provided', () => {
    render(<FindingBadge severity="Warning" count={5} />);
    expect(screen.getByText('Warning')).toBeTruthy();
    expect(screen.getByText('5')).toBeTruthy();
  });

  it('handles case-insensitive severities and custom classes', () => {
    render(<FindingBadge severity="critical" className="custom-class" />);
    const badge = screen.getByTestId('finding-badge');
    expect(badge.className).toContain('custom-class');
    expect(screen.getByText('critical')).toBeTruthy();
  });
});

describe('TurtleAvatar Component', () => {
  it('renders SVG avatar for each character role', () => {
    const characters = ['speedy', 'sheldon', 'sensei', 'judge'];
    for (const char of characters) {
      const { unmount } = render(<TurtleAvatar character={char} />);
      const avatar = screen.getByTestId(`turtle-avatar-${char}`);
      expect(avatar).toBeTruthy();
      expect(avatar.tagName.toLowerCase()).toBe('svg');
      unmount();
    }
  });

  it('renders with different animation states', () => {
    const states = ['idle', 'thinking', 'finding', 'completed', 'failed'] as const;
    for (const state of states) {
      const { unmount } = render(<TurtleAvatar character="speedy" state={state} />);
      const avatar = screen.getByTestId('turtle-avatar-speedy');
      expect(avatar.getAttribute('data-state')).toBe(state);
      unmount();
    }
  });

  it('accepts custom size and className', () => {
    render(<TurtleAvatar character="sheldon" size={96} className="shadow-lg" />);
    const avatar = screen.getByTestId('turtle-avatar-sheldon');
    expect(avatar.getAttribute('width')).toBe('96');
    expect(avatar.getAttribute('height')).toBe('96');
    expect(avatar.getAttribute('class')).toContain('shadow-lg');
  });
});

describe('PersonaCard Component', () => {
  const sampleFindings: ReviewFinding[] = [
    {
      persona: 'AllocationsPerformance',
      severity: 'Warning',
      title: 'Boxing Allocation in Loop',
      detail: 'Value type int boxed to object inside hot loop',
      location: 'OrderProcessor.cs:42',
      citedSymbolFqns: ['System.Int32', 'OrderProcessor.Process'],
    },
    {
      persona: 'AllocationsPerformance',
      severity: 'Critical',
      title: 'Large Object Heap Allocation',
      detail: 'Array allocated exceeds 85,000 bytes threshold',
      location: 'BufferPool.cs:18',
      citedSymbolFqns: ['System.Byte[]'],
    },
  ];

  it('renders persona character info and title', () => {
    render(
      <PersonaCard
        character={TURTLE_CHARACTERS.speedy}
        status="idle"
      />
    );
    expect(screen.getByText('Speedy')).toBeTruthy();
    expect(screen.getByText('The Performance Freak')).toBeTruthy();
    expect(screen.getByText('Idle')).toBeTruthy();
  });

  it('displays live speech bubble quip when provided', () => {
    render(
      <PersonaCard
        character={TURTLE_CHARACTERS.speedy}
        status="running"
        quip="BOXING?! You are allocating on the heap!"
      />
    );
    expect(screen.getByTestId('speech-bubble')).toBeTruthy();
    expect(screen.getByText('BOXING?! You are allocating on the heap!')).toBeTruthy();
  });

  it('renders findings list with badges, locations, and cited symbols', () => {
    render(
      <PersonaCard
        character={TURTLE_CHARACTERS.speedy}
        status="completed"
        findings={sampleFindings}
      />
    );
    expect(screen.getByText('Boxing Allocation in Loop')).toBeTruthy();
    expect(screen.getByText('Large Object Heap Allocation')).toBeTruthy();
    expect(screen.getByText('OrderProcessor.cs:42')).toBeTruthy();
    expect(screen.getByText('BufferPool.cs:18')).toBeTruthy();
    expect(screen.getByText('OrderProcessor.Process')).toBeTruthy();
    expect(screen.getByText('Completed (2)')).toBeTruthy();
  });

  it('renders empty state message when no findings exist', () => {
    render(
      <PersonaCard
        character={TURTLE_CHARACTERS.sheldon}
        status="idle"
        findings={[]}
      />
    );
    expect(screen.getByText(/No findings yet/i)).toBeTruthy();
  });

  it('renders failed state with error message', () => {
    render(
      <PersonaCard
        character={TURTLE_CHARACTERS.sensei}
        status="failed"
        quip="Roslyn parsing failed"
      />
    );
    expect(screen.getByText('Failed')).toBeTruthy();
    expect(screen.getByText('Roslyn parsing failed')).toBeTruthy();
  });
});

describe('CouncilChamber Component', () => {
  const sampleStates: Record<string, PersonaState> = {
    AllocationsPerformance: {
      status: 'running',
      findingCount: 1,
      quip: 'Analyzing memory allocations...',
    },
    SecurityAuditor: {
      status: 'idle',
      findingCount: 0,
    },
    IdiomaticArchitect: {
      status: 'completed',
      findingCount: 2,
      quip: 'Architecture aligns with zen principles.',
    },
  };

  const allFindings: ReviewFinding[] = [
    {
      persona: 'AllocationsPerformance',
      severity: 'Warning',
      title: 'Closure captures variable',
      detail: 'Delegate causes heap allocation',
      location: 'Query.cs:15',
      citedSymbolFqns: ['Query.Filter'],
    },
    {
      persona: 'IdiomaticArchitect',
      severity: 'Nit',
      title: 'Use primary constructor',
      detail: 'Modern C# 12 primary constructors are cleaner',
      location: 'UserDto.cs:4',
      citedSymbolFqns: ['UserDto'],
    },
    {
      persona: 'IdiomaticArchitect',
      severity: 'Warning',
      title: 'Captive Dependency',
      detail: 'Singleton holds Scoped service',
      location: 'ServiceCollection.cs:88',
      citedSymbolFqns: ['IServiceCollection'],
    },
  ];

  it('renders all three council member cards side-by-side', () => {
    render(
      <CouncilChamber
        personaStates={sampleStates}
        findings={allFindings}
      />
    );

    expect(screen.getByText('Speedy')).toBeTruthy();
    expect(screen.getByText('Sheldon')).toBeTruthy();
    expect(screen.getByText('Sensei')).toBeTruthy();
  });

  it('displays aggregate council statistics', () => {
    render(
      <CouncilChamber
        personaStates={sampleStates}
        findings={allFindings}
      />
    );

    expect(screen.getByTestId('council-stats-total')).toBeTruthy();
    expect(screen.getByTestId('council-stats-total').textContent).toContain('3');
    expect(screen.getByTestId('council-stats-active')).toBeTruthy();
  });

  it('routes findings to appropriate persona card', () => {
    render(
      <CouncilChamber
        personaStates={sampleStates}
        findings={allFindings}
      />
    );

    expect(screen.getByText('Closure captures variable')).toBeTruthy();
    expect(screen.getByText('Use primary constructor')).toBeTruthy();
    expect(screen.getByText('Captive Dependency')).toBeTruthy();
  });
});
