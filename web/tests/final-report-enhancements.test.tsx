import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import React from 'react';
import { RiskTagBadge, detectRiskTag } from '@/components/RiskTagBadge';
import { FindingCard } from '@/components/FindingCard';
import { FinalReportView } from '@/components/FinalReportView';
import type { ReviewFinding, CouncilVerdict } from '@/lib/types';

describe('RiskTagBadge & detectRiskTag', () => {
  it('detects defect classes accurately from title and detail', () => {
    expect(detectRiskTag('Raw SQL Injection', 'concatenation into string')).toBe('sql-injection');
    expect(detectRiskTag('Path Traversal', 'unvalidated Path.Combine')).toBe('path-traversal');
    expect(detectRiskTag('Hardcoded Secret', 'sk_live in ApiKey')).toBe('hardcoded-secret');
    expect(detectRiskTag('Sync-over-async', 'blocking on .Result')).toBe('sync-over-async');
    expect(detectRiskTag('Async Issue', 'missing ConfigureAwait(false)')).toBe('missing-configureawait');
    expect(detectRiskTag('Captive Dependency', 'Singleton capturing scoped service')).toBe('captive-dependency');
    expect(detectRiskTag('Architecture Violation', 'service locator via IServiceProvider')).toBe('service-locator');
    expect(detectRiskTag('Error handling', 'swallowed exception in catch block')).toBe('swallowed-exception');
    expect(detectRiskTag('Allocation', 'implicit boxing of integer')).toBe('boxing-allocation');
    expect(detectRiskTag('LINQ', 'closure capture allocates display class')).toBe('linq-closure');
  });

  it('renders risk badge with uppercase label and styling', () => {
    render(<RiskTagBadge tag="sql-injection" />);
    const badge = screen.getByTestId('risk-tag-badge');
    expect(badge).toBeTruthy();
    expect(badge.textContent).toContain('SQL Injection');
    expect(badge.className).toContain('rose');
  });
});

describe('FindingCard Component', () => {
  const sampleFinding: ReviewFinding = {
    persona: 'SecurityAuditor',
    severity: 'Critical',
    title: 'SQL Injection Vulnerability',
    detail: 'Raw user input interpolated into command text.',
    location: 'Services/OrderRepository.cs:16',
    citedSymbolFqns: ['Shop.Services.OrderRepository.FindByCustomerAsync', 'Shop.Fake.NonExistentHelper'],
  };

  const sampleAudit = [
    { citedFqn: 'Shop.Services.OrderRepository.FindByCustomerAsync', verified: true, evidence: 'Payload' as const },
    { citedFqn: 'Shop.Fake.NonExistentHelper', verified: false },
  ];

  it('renders risk badge, severity, location, and compiler provenance chips', () => {
    render(<FindingCard finding={sampleFinding} guardAudit={sampleAudit} />);

    // Finding severity and heading title
    expect(screen.getByText('Critical')).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'SQL Injection Vulnerability' })).toBeTruthy();

    // Risk tag badge
    expect(screen.getByTestId('risk-tag-badge')).toBeTruthy();
    expect(screen.getByText('SQL Injection')).toBeTruthy();

    // Location button
    expect(screen.getByText('Services/OrderRepository.cs:16')).toBeTruthy();

    // Provenance chips: one verified, one hallucination
    const chips = screen.getAllByTestId('symbol-provenance-chip');
    expect(chips).toHaveLength(2);

    expect(chips[0].getAttribute('data-verified')).toBe('true');
    expect(chips[0].textContent).toContain('✓ Roslyn');

    expect(chips[1].getAttribute('data-verified')).toBe('false');
    expect(chips[1].className).toContain('line-through');
  });

  it('copies location to clipboard on click', async () => {
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, {
      clipboard: { writeText: writeTextMock },
    });

    render(<FindingCard finding={sampleFinding} guardAudit={sampleAudit} />);
    const locBtn = screen.getByTitle('Click to copy file and line location');
    await act(async () => { fireEvent.click(locBtn); });

    expect(writeTextMock).toHaveBeenCalledWith('Services/OrderRepository.cs:16');
  });
});

describe('FinalReportView Interactive Filtering', () => {
  const sampleVerdict: CouncilVerdict = {
    personas: [
      {
        persona: 'AllocationsPerformance',
        findings: [
          {
            persona: 'AllocationsPerformance',
            severity: 'Warning',
            title: 'Missing ConfigureAwait',
            detail: 'Async call without ConfigureAwait(false).',
            location: 'Services/PaymentService.cs:19',
            citedSymbolFqns: ['Shop.Services.PaymentService.PayAsync'],
          },
        ],
      },
      {
        persona: 'SecurityAuditor',
        findings: [
          {
            persona: 'SecurityAuditor',
            severity: 'Critical',
            title: 'Dynamic SQL Injection',
            detail: 'Unsanitized input in query.',
            location: 'Services/OrderRepository.cs:16',
            citedSymbolFqns: ['Shop.Services.OrderRepository.FindByCustomerAsync'],
          },
        ],
      },
    ],
    synthesized: [
      {
        persona: 'AllocationsPerformance',
        severity: 'Warning',
        title: 'Missing ConfigureAwait',
        detail: 'Async call without ConfigureAwait(false).',
        location: 'Services/PaymentService.cs:19',
        citedSymbolFqns: ['Shop.Services.PaymentService.PayAsync'],
      },
      {
        persona: 'SecurityAuditor',
        severity: 'Critical',
        title: 'Dynamic SQL Injection',
        detail: 'Unsanitized input in query.',
        location: 'Services/OrderRepository.cs:16',
        citedSymbolFqns: ['Shop.Services.OrderRepository.FindByCustomerAsync'],
      },
    ],
    guardAudit: [
      { citedFqn: 'Shop.Services.PaymentService.PayAsync', verified: true, evidence: 'Payload' as const },
      { citedFqn: 'Shop.Services.OrderRepository.FindByCustomerAsync', verified: true, evidence: 'Payload' as const },
    ],
  };

  it('renders all synthesized findings by default', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown="# Report" />);
    expect(screen.getByRole('heading', { name: 'Missing ConfigureAwait' })).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Dynamic SQL Injection' })).toBeTruthy();
  });

  it('filters findings by severity', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown="# Report" />);

    // Click Critical filter
    const critBtn = screen.getByTestId('filter-severity-critical');
    fireEvent.click(critBtn);

    expect(screen.getByRole('heading', { name: 'Dynamic SQL Injection' })).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Missing ConfigureAwait' })).toBeNull();

    // Click Warning filter
    const warnBtn = screen.getByTestId('filter-severity-warning');
    fireEvent.click(warnBtn);

    expect(screen.getByRole('heading', { name: 'Missing ConfigureAwait' })).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Dynamic SQL Injection' })).toBeNull();
  });

  it('filters findings by search query', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown="# Report" />);

    const searchInput = screen.getByTestId('finding-search-input');
    fireEvent.change(searchInput, { target: { value: 'PaymentService' } });

    expect(screen.getByRole('heading', { name: 'Missing ConfigureAwait' })).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Dynamic SQL Injection' })).toBeNull();
  });
});
