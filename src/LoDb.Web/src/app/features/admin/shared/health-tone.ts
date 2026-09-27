import type { Tone } from '../widgets/badge';

// The states of a probe of the monitoring, as the API names them.
const HEALTH_TONES: Readonly<Record<string, Tone>> = { ok: 'good', degraded: 'warn', down: 'bad' };

/** The tone of the badge of a probe's state; muted for a state the admin does not know. */
export function healthTone(status: string): Tone {
  return HEALTH_TONES[status] ?? 'muted';
}
