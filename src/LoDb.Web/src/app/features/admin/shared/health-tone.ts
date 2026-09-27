import type { Tone } from '../widgets/badge';

// The states of a probe of the monitoring, as the API names them.
const HEALTH_TONES: Readonly<Record<string, Tone>> = { ok: 'good', degraded: 'warn', down: 'bad' };

/**
 * The tone of the badge of a probe's state. A state the admin does not know reads as a
 * failure, as in the legacy admin: a probe that answers something unexpected is not healthy.
 */
export function healthTone(status: string): Tone {
  return HEALTH_TONES[status] ?? 'bad';
}
