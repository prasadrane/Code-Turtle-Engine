import type { PersonaRole } from './types';

export interface CharacterColorTheme {
  primary: string;
  badge: string;
  border: string;
  glow: string;
  accent: string;
}

export interface TurtleCharacter {
  id: string;
  name: string;
  title: string;
  role: PersonaRole;
  modelRole: 'fast' | 'deep' | 'deterministic';
  icon: string;
  avatar: string;
  color: CharacterColorTheme;
  bio: string;
  quips: string[];
  focusAreas: string[];
}

export const TURTLE_CHARACTERS: Record<string, TurtleCharacter> = {
  speedy: {
    id: 'speedy',
    name: 'Speedy',
    title: 'The Performance Freak',
    role: 'AllocationsPerformance',
    modelRole: 'fast',
    icon: '⚡',
    avatar: '/turtles/speedy.svg',
    color: {
      primary: 'amber',
      badge: 'bg-amber-500/20 text-amber-300 border-amber-500/40',
      border: 'border-amber-500/30',
      glow: 'shadow-amber-500/20',
      accent: 'text-amber-400',
    },
    bio: 'Hyper-caffeinated and freaks out about allocations. Carries a tiny stopwatch and panics over boxing and GC pressure.',
    quips: [
      "BOXING?! You're allocating on the heap for an INT? My shell is tingling!",
      'This LINQ closure is capturing a local — do you KNOW what the GC has to do?!',
      'Missing ConfigureAwait(false)? My stopwatch is weeping synchronous tears!',
      'Zero allocations in this method? Now THAT is aerodynamic!',
    ],
    focusAreas: ['LOH risk', 'Closures', 'Boxing', 'Unawaited tasks', 'ConfigureAwait', 'Concurrency'],
  },
  sheldon: {
    id: 'sheldon',
    name: 'Sheldon',
    title: 'The Security Paranoid',
    role: 'SecurityAuditor',
    modelRole: 'deep',
    icon: '🔒',
    avatar: '/turtles/sheldon.svg',
    color: {
      primary: 'rose',
      badge: 'bg-rose-500/20 text-rose-300 border-rose-500/40',
      border: 'border-rose-500/30',
      glow: 'shadow-rose-500/20',
      accent: 'text-rose-400',
    },
    bio: 'Deeply suspicious, wears dark sunglasses, and trusts nobody. Whispers dramatically about injection vectors and hardcoded secrets.',
    quips: [
      'String concatenation in a SQL query... I need to sit down.',
      'Is that... an API key... IN THE SOURCE CODE?!',
      'Unvalidated user input traveling straight to the datastore. The horror.',
      'The perimeter is secured. For now. I am watching you.',
    ],
    focusAreas: ['SQL injection', 'Input sanitization', 'Auth bypass', 'Secrets', 'Dependency risks'],
  },
  sensei: {
    id: 'sensei',
    name: 'Sensei',
    title: 'The Idiomatic Architect',
    role: 'IdiomaticArchitect',
    modelRole: 'deep',
    icon: '🏯',
    avatar: '/turtles/sensei.svg',
    color: {
      primary: 'violet',
      badge: 'bg-violet-500/20 text-violet-300 border-violet-500/40',
      border: 'border-violet-500/30',
      glow: 'shadow-violet-500/20',
      accent: 'text-violet-400',
    },
    bio: 'Calm zen master who quotes design principles and feels quiet disappointment when encountering code smells and broken DI lifetimes.',
    quips: [
      'A Singleton capturing a Transient... the circle of dependency suffering continues.',
      "Your naming tells a story. Sadly, it's a mystery novel.",
      'Clean abstractions bring inner peace to the entire assembly.',
      'Interface segregation is not merely a rule; it is harmony.',
    ],
    focusAreas: ['Modern C#', 'DI lifetimes', 'Captive dependencies', 'Clean architecture', 'Naming patterns'],
  },
  judge: {
    id: 'judge',
    name: 'Judge Shellsworth',
    title: 'The Arbiter',
    role: 'Arbiter',
    modelRole: 'deterministic',
    icon: '⚖️',
    avatar: '/turtles/judge.svg',
    color: {
      primary: 'sky',
      badge: 'bg-sky-500/20 text-sky-300 border-sky-500/40',
      border: 'border-sky-500/30',
      glow: 'shadow-sky-500/20',
      accent: 'text-sky-400',
    },
    bio: 'Stern but fair presider of the Turtle Council. Wields a legendary gavel to merge duplicate findings and compiler-verify every citation.',
    quips: [
      'Order in the chamber! Let me review the evidence...',
      'All persona findings reviewed and merged.',
      'The Roslyn compiler never lies. Hallucinations have been struck from the record!',
      'The shell holds firm. Truth in citations preserved.',
    ],
    focusAreas: ['Deduplication', 'Roslyn citation verification', 'Zero hallucination audit', 'Synthesis'],
  },
};

export const CHARACTERS_LIST: TurtleCharacter[] = Object.values(TURTLE_CHARACTERS);

export function getCharacterByPersona(role: string): TurtleCharacter | undefined {
  return CHARACTERS_LIST.find(
    (c) => c.role.toLowerCase() === role.toLowerCase()
  );
}

export function getCharacterByName(name: string): TurtleCharacter | undefined {
  return CHARACTERS_LIST.find(
    (c) => c.name.toLowerCase() === name.toLowerCase()
  );
}

export function formatPersonaCompletedQuip(role: string, findingCount: number): string {
  const norm = role.toLowerCase();
  if (norm.includes('allocation') || norm.includes('performance')) {
    return `${findingCount} allocation(s) found! My stopwatch is crying.`;
  }
  if (norm.includes('security')) {
    return `${findingCount} security consideration(s) detected.`;
  }
  if (norm.includes('architect') || norm.includes('idiomatic')) {
    return `${findingCount} architectural observation(s) noted.`;
  }
  return `${findingCount} finding(s) identified.`;
}
