import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { PhaseTimeline } from '@/components/PhaseTimeline';
import { TrustAuditVisualizer } from '@/components/TrustAuditVisualizer';
import { LanguageRejectionModal } from '@/components/LanguageRejectionModal';
import { FinalReportView } from '@/components/FinalReportView';
import type { GuardResult, CouncilVerdict, ReviewFinding } from '@/lib/types';

describe('PhaseTimeline Component', () => {
  it('renders all 6 pipeline phases', () => {
    render(<PhaseTimeline currentPhase="compiling" />);
    expect(screen.getByText('Validating')).toBeTruthy();
    expect(screen.getByText('Cloning')).toBeTruthy();
    expect(screen.getByText('Compiling')).toBeTruthy();
    expect(screen.getByText('Deliberating')).toBeTruthy();
    expect(screen.getByText('Auditing')).toBeTruthy();
    expect(screen.getByText('Complete')).toBeTruthy();
  });

  it('marks prior phases as completed and current phase as active', () => {
    render(<PhaseTimeline currentPhase="compiling" />);
    const compilingStep = screen.getByTestId('phase-step-compiling');
    expect(compilingStep.getAttribute('data-status')).toBe('active');

    const validatingStep = screen.getByTestId('phase-step-validating');
    expect(validatingStep.getAttribute('data-status')).toBe('completed');

    const cloningStep = screen.getByTestId('phase-step-cloning');
    expect(cloningStep.getAttribute('data-status')).toBe('completed');

    const deliberatingStep = screen.getByTestId('phase-step-deliberating');
    expect(deliberatingStep.getAttribute('data-status')).toBe('pending');
  });

  it('renders active phase message when provided', () => {
    render(
      <PhaseTimeline
        currentPhase="cloning"
        phaseMessage="Cloning repository code-turtle/sample-service..."
      />
    );
    expect(screen.getByText('Cloning repository code-turtle/sample-service...')).toBeTruthy();
  });

  it('supports vertical orientation layout', () => {
    const { container } = render(
      <PhaseTimeline currentPhase="deliberating" orientation="vertical" />
    );
    const stepper = container.querySelector('[data-testid="phase-timeline"]');
    expect(stepper?.getAttribute('data-orientation')).toBe('vertical');
  });
});

describe('TrustAuditVisualizer Component', () => {
  const sampleAudit: GuardResult[] = [
    { citedFqn: 'SampleRepo.PaymentService.ProcessPaymentAsync', verified: true },
    { citedFqn: 'System.Linq.Enumerable.Where', verified: true },
    { citedFqn: 'SampleRepo.IPaymentGateway.ChargeAsync', verified: true },
    { citedFqn: 'SampleRepo.NonExistentHelper.Sanitize', verified: false },
  ];

  it('renders Judge Shellsworth hero header and Roslyn AST explainer', () => {
    render(<TrustAuditVisualizer guardAudit={sampleAudit} />);
    expect(screen.getByText(/Judge Shellsworth/i)).toBeTruthy();
    expect(screen.getByTestId('roslyn-explainer')).toBeTruthy();
    expect(screen.getAllByText(/Roslyn compiler/i).length).toBeGreaterThan(0);
  });

  it('calculates Trust Score shield metric correctly', () => {
    render(<TrustAuditVisualizer guardAudit={sampleAudit} />);
    // 3 out of 4 verified = 75%
    expect(screen.getByTestId('trust-score-badge')).toBeTruthy();
    expect(screen.getByText(/75%/i)).toBeTruthy();
    expect(screen.getByText(/3 Verified/i)).toBeTruthy();
    expect(screen.getByText(/1 Stripped/i)).toBeTruthy();
  });

  it('renders symbol verification table with verified and stripped rows', () => {
    render(<TrustAuditVisualizer guardAudit={sampleAudit} />);
    expect(screen.getByText('SampleRepo.PaymentService.ProcessPaymentAsync')).toBeTruthy();
    expect(screen.getByText('SampleRepo.NonExistentHelper.Sanitize')).toBeTruthy();

    const verifiedBadges = screen.getAllByTestId('status-verified');
    expect(verifiedBadges.length).toBe(3);

    const strippedBadges = screen.getAllByTestId('status-stripped');
    expect(strippedBadges.length).toBe(1);
    expect(screen.getByText(/Hallucination Stripped/i)).toBeTruthy();
  });

  it('renders 100% Trust Score when all citations are verified', () => {
    const cleanAudit: GuardResult[] = [
      { citedFqn: 'System.String', verified: true },
      { citedFqn: 'System.Int32', verified: true },
    ];
    render(<TrustAuditVisualizer guardAudit={cleanAudit} />);
    expect(screen.getByText(/100%/i)).toBeTruthy();
    expect(screen.getByText(/0 Stripped/i)).toBeTruthy();
  });
});

describe('LanguageRejectionModal Component', () => {
  it('renders non-.NET rejection modal with humorous message and retreating turtle', () => {
    render(
      <LanguageRejectionModal
        isOpen={true}
        type="not_dotnet"
        detectedLanguage="python"
        onClose={vi.fn()}
      />
    );
    expect(screen.getByTestId('language-rejection-modal')).toBeTruthy();
    expect(screen.getAllByText(/Python/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/curly braces/i)).toBeTruthy();
    expect(screen.getByTestId('retreating-turtle')).toBeTruthy();
    expect(screen.getByText(/Coming Soon/i)).toBeTruthy();
  });

  it('renders private repository velvet rope modal with security turtle', () => {
    render(
      <LanguageRejectionModal
        isOpen={true}
        type="private_repo"
        onClose={vi.fn()}
      />
    );
    expect(screen.getByTestId('private-repo-modal')).toBeTruthy();
    expect(screen.getByText(/Halt! This repository appears to be private/i)).toBeTruthy();
    expect(screen.getByTestId('security-turtle')).toBeTruthy();
    expect(screen.getAllByText(/velvet rope/i).length).toBeGreaterThan(0);
  });

  it('calls onClose when dismiss button is clicked', () => {
    const handleClose = vi.fn();
    render(
      <LanguageRejectionModal
        isOpen={true}
        type="not_dotnet"
        detectedLanguage="javascript"
        onClose={handleClose}
      />
    );
    const closeBtn = screen.getByTestId('modal-close-btn');
    fireEvent.click(closeBtn);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('does not render modal content when isOpen is false', () => {
    const { container } = render(
      <LanguageRejectionModal isOpen={false} type="not_dotnet" onClose={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });
});

describe('FinalReportView Component', () => {
  const sampleFindings: ReviewFinding[] = [
    {
      persona: 'AllocationsPerformance',
      severity: 'Warning',
      title: 'LINQ Closure Allocation',
      detail: 'Local variable threshold captured by closure',
      location: 'PaymentService.cs:21',
      citedSymbolFqns: ['SampleRepo.PaymentService.ProcessPaymentAsync'],
    },
    {
      persona: 'SecurityAuditor',
      severity: 'Critical',
      title: 'SQL Injection Vector',
      detail: 'Concatenation into SQL query',
      location: 'DataAccess.cs:8',
      citedSymbolFqns: ['SampleRepo.DataAccess.BuildQuery'],
    },
  ];

  const sampleVerdict: CouncilVerdict = {
    personas: [
      {
        persona: 'AllocationsPerformance',
        findings: [sampleFindings[0]],
      },
      {
        persona: 'SecurityAuditor',
        findings: [sampleFindings[1]],
      },
    ],
    synthesized: sampleFindings,
    guardAudit: [
      { citedFqn: 'SampleRepo.PaymentService.ProcessPaymentAsync', verified: true },
      { citedFqn: 'SampleRepo.DataAccess.BuildQuery', verified: true },
    ],
  };

  const sampleMarkdown = '# 🐢 Code-Turtle Review Report\n\n**Verdict:** Changes require revision.\n- Critical SQL injection found.';

  it('renders 3 tabs and defaults to synthesized verdict', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown={sampleMarkdown} />);
    expect(screen.getByRole('tab', { name: /Synthesized Verdict/i })).toBeTruthy();
    expect(screen.getByRole('tab', { name: /Persona Breakdown/i })).toBeTruthy();
    expect(screen.getByRole('tab', { name: /Markdown Review/i })).toBeTruthy();

    expect(screen.getByText('LINQ Closure Allocation')).toBeTruthy();
    expect(screen.getByText('SQL Injection Vector')).toBeTruthy();
  });

  it('switches to Persona Breakdown tab and displays persona findings', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown={sampleMarkdown} />);
    const personasTab = screen.getByRole('tab', { name: /Persona Breakdown/i });
    fireEvent.click(personasTab);

    expect(screen.getByTestId('persona-breakdown-panel')).toBeTruthy();
    expect(screen.getByText('Speedy')).toBeTruthy();
    expect(screen.getByText('Sheldon')).toBeTruthy();
  });

  it('switches to Raw Markdown tab and supports copying report', () => {
    render(<FinalReportView verdict={sampleVerdict} markdown={sampleMarkdown} />);
    const markdownTab = screen.getByRole('tab', { name: /Markdown Review/i });
    fireEvent.click(markdownTab);

    expect(screen.getByTestId('markdown-review-panel')).toBeTruthy();
    expect(screen.getByText(/# 🐢 Code-Turtle Review Report/i)).toBeTruthy();
    expect(screen.getByRole('button', { name: /copy/i })).toBeTruthy();
  });
});
