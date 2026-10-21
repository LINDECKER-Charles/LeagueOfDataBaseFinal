import type { Tone } from '../../../widgets/badge';

// The states of a pass (running, paused, completed, cancelled), of a unit (pending, running,
// completed, failed, and stopped when a pause left it running) and of an epoch of the store
// (served, writing, standby, retired), as the API names them: the sets agree where they meet.
const TONES: Readonly<Record<string, Tone>> = {
  running: 'good',
  paused: 'warn',
  completed: 'good',
  cancelled: 'muted',
  pending: 'muted',
  failed: 'bad',
  stopped: 'warn',
  served: 'good',
  writing: 'warn',
  standby: 'muted',
  // Retired, it still costs until the lifecycle deletes it: one to keep an eye on.
  retired: 'warn',
};

/**
 * The tone of a state of the pre-rendering. One the admin does not know reads as a failure:
 * an API that answers something unexpected is not one to trust.
 */
export function prerenderTone(state: string): Tone {
  return TONES[state] ?? 'bad';
}
