import artifact from './workspace-changes.v1.json';
import replay from './workspace-replay.v1.json';
import type { ReviewChanges } from '@/lib/changes';
import type { ReplayEventItem } from '@/lib/types';

// Authored demo only. The original demo-replay.json remains historical evidence.
export const workspaceChanges = artifact as ReviewChanges;
export const workspaceReplay = replay as ReplayEventItem[];
