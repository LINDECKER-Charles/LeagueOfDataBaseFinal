import type { PrerenderPaceView } from '../../../../../../../../core/api/generated/models/prerender-pace-view';
import { figure } from '../../../../../../format/figure';
import { stamp } from '../../../../../../format/stamp';
import type { AdminText } from '../../../../../../shared/inject-admin-text';

const REGULATOR = 'prerender.live.pace.regulator';
const REASONS = 'prerender.live.pace.reasons';
const HOST_CPU = 'host-cpu';
const HOST_CPU_BUSY = 'host-cpu-busy';
const CEILING = 'ceiling';
const SEPARATOR = ' · ';
// The states and the reasons the API names today: anything else is not shown, never a raw token.
const STATES: ReadonlySet<string> = new Set([
  'starting',
  'climbing',
  'steady',
  'slowed',
  'held',
  'fixed',
  'manual',
]);
const REASON_KEYS: ReadonlySet<string> = new Set([
  'warming-up',
  CEILING,
  HOST_CPU,
  HOST_CPU_BUSY,
  'host-cpu-sustained',
  'host-steal',
  'visitors-ssr',
  'visitors-refused',
  'api-cpu',
  'pass-latency',
  'pass-ssr-pressure',
  'signal-missing',
  'fixed',
  'manual',
]);

function reasonOf(pace: PrerenderPaceView, cpu: string | null, texts: AdminText): string | null {
  // The busy reason shows the figure when the regulator has one, and reads plainly when not.
  const token = pace.reason === HOST_CPU && cpu === null ? HOST_CPU_BUSY : pace.reason;
  if (!token || !REASON_KEYS.has(token)) {
    return null;
  }
  return texts.text(`${REASONS}.${token}`, { cpu });
}

function withCpu(line: string, cpu: string | null, texts: AdminText): string {
  return cpu === null ? line : `${line}${SEPARATOR}${texts.text(`${REGULATOR}.cpu`, { cpu })}`;
}

/**
 * The line under the pace that says what the regulator of the pass does and why (§ 4.3):
 * where the pace stands, and the worst signal when it holds it back. `null` for a state or a
 * reason this screen does not know, so that no raw token reaches the screen.
 */
export function regulatorLine(pace: PrerenderPaceView, texts: AdminText): string | null {
  if (!STATES.has(pace.state)) {
    return null;
  }
  const rate = pace.rendersPerMinute;
  const cpu = pace.hostCpu == null ? null : figure(pace.hostCpu, 'share');
  const say = (key: string, params: Record<string, unknown>) =>
    texts.text(`${REGULATOR}.${key}`, params);
  switch (pace.state) {
    case 'starting':
      return say('starting', { rate });
    case 'climbing':
      return withCpu(say('climbing', { rate }), cpu, texts);
    case 'fixed':
      return say('fixed', { rate });
    case 'manual':
      return say('manual', { rate });
    default:
      return reasoned(pace, cpu, texts);
  }
}

// Steady at the ceiling, slowed and held, each told with its reason.
function reasoned(pace: PrerenderPaceView, cpu: string | null, texts: AdminText): string | null {
  const rate = pace.rendersPerMinute;
  if (pace.state === 'steady' && pace.reason === CEILING) {
    return withCpu(texts.text(`${REGULATOR}.ceiling`, { rate }), cpu, texts);
  }
  const reason = reasonOf(pace, cpu, texts);
  if (reason === null) {
    return null;
  }
  if (pace.state === 'held') {
    const at = stamp(pace.heldUntil, 'minute');
    return texts.text(`${REGULATOR}.held`, { at, reason });
  }
  return texts.text(`${REGULATOR}.${pace.state}`, { rate, reason });
}
