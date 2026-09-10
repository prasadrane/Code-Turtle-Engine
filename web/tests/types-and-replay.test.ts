import { describe, it, expect } from 'vitest';
import replayData from '@/data/demo-replay.json';
import type {
  ReplayEventItem,
  PhaseEventData,
  PersonaStartedEventData,
  PersonaFindingEventData,
  PersonaCompletedEventData,
  ArbiterMergeEventData,
  GuardAuditEventData,
  CompletedEventData,
} from '@/lib/types';

describe('Demo Replay Data & Schema Integrity', () => {
  const items = replayData as ReplayEventItem[];

  it('contains an ordered list of replay events with monotonically non-decreasing delays', () => {
    expect(items.length).toBeGreaterThanOrEqual(10);
    let prevDelay = 0;
    for (const item of items) {
      expect(typeof item.delay).toBe('number');
      expect(item.delay).toBeGreaterThanOrEqual(prevDelay);
      expect(item.event).toBeDefined();
      expect(typeof item.event.type).toBe('string');
      prevDelay = item.delay;
    }
  });

  it('covers early pipeline phases (validating, cloning, compiling)', () => {
    const phases = items
      .filter((i) => i.event.type === 'phase')
      .map((i) => (i.event as PhaseEventData).phase);

    expect(phases).toContain('validating');
    expect(phases).toContain('cloning');
    expect(phases).toContain('compiling');
  });

  it('starts all three parallel personas (Speedy, Sheldon, Sensei)', () => {
    const started = items
      .filter((i) => i.event.type === 'persona_started')
      .map((i) => (i.event as PersonaStartedEventData).persona);

    expect(started).toContain('AllocationsPerformance');
    expect(started).toContain('SecurityAuditor');
    expect(started).toContain('IdiomaticArchitect');
  });

  it('contains realistic SampleRepo findings for each council member', () => {
    const findings = items
      .filter((i) => i.event.type === 'persona_finding')
      .map((i) => (i.event as PersonaFindingEventData).finding);

    expect(findings.length).toBeGreaterThanOrEqual(3);

    // Speedy finding (allocations / async / boxing)
    const speedyFinding = findings.find((f) => f.persona === 'AllocationsPerformance');
    expect(speedyFinding).toBeDefined();
    expect(speedyFinding?.location).toContain('PaymentService.cs');
    expect(speedyFinding?.citedSymbolFqns.length).toBeGreaterThan(0);

    // Sheldon finding (SQL injection)
    const sheldonFinding = findings.find((f) => f.persona === 'SecurityAuditor');
    expect(sheldonFinding).toBeDefined();
    expect(sheldonFinding?.location).toContain('DataAccess.cs');
    expect(sheldonFinding?.severity).toMatch(/Error|Critical/);

    // Sensei finding (Captive dependency / DI lifetime)
    const senseiFinding = findings.find((f) => f.persona === 'IdiomaticArchitect');
    expect(senseiFinding).toBeDefined();
    expect(senseiFinding?.location).toContain('DiRegistration.cs');
  });

  it('completes all three personas with quips', () => {
    const completed = items
      .filter((i) => i.event.type === 'persona_completed')
      .map((i) => i.event as PersonaCompletedEventData);

    expect(completed).toHaveLength(3);
    for (const c of completed) {
      expect(c.findingCount).toBeGreaterThanOrEqual(1);
      expect(c.quip).toBeTruthy();
    }
  });

  it('executes Arbiter deduplication and merge', () => {
    const arbiterMerge = items
      .filter((i) => i.event.type === 'arbiter_merge')
      .map((i) => i.event as ArbiterMergeEventData)[0];

    expect(arbiterMerge).toBeDefined();
    expect(arbiterMerge.character).toBe('Judge Shellsworth');
    expect(arbiterMerge.totalRaw).toBeGreaterThanOrEqual(arbiterMerge.afterDedupe);
  });

  it('executes Trust Audit showing verified compiler symbols and stripped hallucinations', () => {
    const guardAudit = items
      .filter((i) => i.event.type === 'guard_audit')
      .map((i) => i.event as GuardAuditEventData)[0];

    expect(guardAudit).toBeDefined();
    expect(guardAudit.character).toBe('Judge Shellsworth');
    expect(guardAudit.verified).toBeGreaterThan(0);
    expect(guardAudit.stripped).toBeGreaterThan(0);
    expect(guardAudit.total).toBe(guardAudit.verified + guardAudit.stripped);

    const verifiedSymbols = guardAudit.auditDetails.filter((d) => d.verified);
    const strippedSymbols = guardAudit.auditDetails.filter((d) => !d.verified);

    expect(verifiedSymbols.length).toBe(guardAudit.verified);
    expect(strippedSymbols.length).toBe(guardAudit.stripped);
  });

  it('completes with markdown report and council verdict', () => {
    const completed = items
      .filter((i) => i.event.type === 'completed')
      .map((i) => i.event as CompletedEventData)[0];

    expect(completed).toBeDefined();
    expect(completed.markdown.length).toBeGreaterThan(50);
    expect(completed.verdict.synthesized.length).toBeGreaterThan(0);
    expect(completed.verdict.guardAudit.length).toBeGreaterThan(0);
  });
});
