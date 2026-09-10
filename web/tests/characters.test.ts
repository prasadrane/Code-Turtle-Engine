import { describe, it, expect } from 'vitest';
import {
  TURTLE_CHARACTERS,
  CHARACTERS_LIST,
  getCharacterByPersona,
  getCharacterByName,
  formatPersonaCompletedQuip,
} from '@/lib/characters';

describe('Turtle Characters', () => {
  it('defines exactly the 4 turtle council members', () => {
    expect(CHARACTERS_LIST).toHaveLength(4);
    const names = CHARACTERS_LIST.map((c) => c.name);
    expect(names).toContain('Speedy');
    expect(names).toContain('Sheldon');
    expect(names).toContain('Sensei');
    expect(names).toContain('Judge Shellsworth');
  });

  it('provides rich metadata for Speedy (Performance Freak)', () => {
    const speedy = TURTLE_CHARACTERS.speedy;
    expect(speedy).toBeDefined();
    expect(speedy.name).toBe('Speedy');
    expect(speedy.role).toBe('AllocationsPerformance');
    expect(speedy.icon).toBe('⚡');
    expect(speedy.quips.length).toBeGreaterThan(0);
    expect(speedy.bio).toContain('stopwatch');
    expect(speedy.color.badge).toBeDefined();
  });

  it('provides rich metadata for Sheldon (Security Paranoid)', () => {
    const sheldon = TURTLE_CHARACTERS.sheldon;
    expect(sheldon).toBeDefined();
    expect(sheldon.name).toBe('Sheldon');
    expect(sheldon.role).toBe('SecurityAuditor');
    expect(sheldon.icon).toBe('🔒');
    expect(sheldon.quips.length).toBeGreaterThan(0);
    expect(sheldon.bio).toContain('sunglasses');
  });

  it('provides rich metadata for Sensei (Idiomatic Architect)', () => {
    const sensei = TURTLE_CHARACTERS.sensei;
    expect(sensei).toBeDefined();
    expect(sensei.name).toBe('Sensei');
    expect(sensei.role).toBe('IdiomaticArchitect');
    expect(sensei.icon).toBe('🏯');
    expect(sensei.quips.length).toBeGreaterThan(0);
    expect(sensei.bio).toContain('zen');
  });

  it('provides rich metadata for Judge Shellsworth (The Arbiter)', () => {
    const judge = TURTLE_CHARACTERS.judge;
    expect(judge).toBeDefined();
    expect(judge.name).toBe('Judge Shellsworth');
    expect(judge.role).toBe('Arbiter');
    expect(judge.icon).toBe('⚖️');
    expect(judge.quips.length).toBeGreaterThan(0);
  });

  it('looks up characters by PersonaRole correctly', () => {
    expect(getCharacterByPersona('AllocationsPerformance')?.name).toBe('Speedy');
    expect(getCharacterByPersona('SecurityAuditor')?.name).toBe('Sheldon');
    expect(getCharacterByPersona('IdiomaticArchitect')?.name).toBe('Sensei');
    expect(getCharacterByPersona('Arbiter')?.name).toBe('Judge Shellsworth');
    expect(getCharacterByPersona('Unknown' as any)).toBeUndefined();
  });

  it('looks up characters by Name correctly (case-insensitive)', () => {
    expect(getCharacterByName('Speedy')?.role).toBe('AllocationsPerformance');
    expect(getCharacterByName('sheldon')?.role).toBe('SecurityAuditor');
    expect(getCharacterByName('SENSEI')?.role).toBe('IdiomaticArchitect');
    expect(getCharacterByName('Judge Shellsworth')?.role).toBe('Arbiter');
    expect(getCharacterByName('Nonexistent')).toBeUndefined();
  });

  it('formats persona completed quips with counts', () => {
    const speedyQuip = formatPersonaCompletedQuip('AllocationsPerformance', 3);
    expect(speedyQuip).toContain('3');
    expect(speedyQuip.toLowerCase()).toContain('stopwatch');

    const sheldonQuip = formatPersonaCompletedQuip('SecurityAuditor', 1);
    expect(sheldonQuip).toContain('1');

    const senseiQuip = formatPersonaCompletedQuip('IdiomaticArchitect', 2);
    expect(senseiQuip).toContain('2');
  });
});
