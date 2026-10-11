import { readPositiveInteger } from '../read-positive-integer';
import type { RenderLimits } from './render-limits';

type Environment = Readonly<Record<string, string | undefined>>;

// Sized for the production host (4 shared vCPU, one Node thread for the renderer): a render
// costs about 0.1 s of the renderer and as much of the API, which loads a whole catalog for
// each (version, language) it no longer holds. One pinned render a second stays a fraction
// of a core; a crawl of every patch in every language no longer takes the host. The current
// pages get five renders a second, a burst of a minute's worth of their usual misses; one
// client gets two renders at once and a pinned page every ten seconds, which no reader
// outpaces, so that it alone never takes what the others need.
//
// One client also gets a fifth of the current pages' budget, a render a second and twenty at
// once: without it, a client asking for pages nginx has not cached spent the whole lane, and
// every other visitor's uncached page was refused. A visitor's full page load renders once,
// and its clicks inside the site render nothing here (the browser app navigates by itself):
// the readers behind one shared address (a home NAT, a carrier's CGNAT) stay far below it. A
// crawler on one address still gets every page nginx holds in its cache; only its misses past
// one a second get a 503 and its Retry-After.
const DEFAULTS: RenderLimits = {
  maxInFlight: 8,
  pinnedMaxInFlight: 3,
  pinnedPerMinute: 60,
  pinnedBurst: 10,
  currentPerMinute: 300,
  currentBurst: 60,
  perClientMaxInFlight: 2,
  pinnedPerClientPerMinute: 6,
  currentPerClientPerMinute: 60,
  currentPerClientBurst: 20,
  trackedClients: 50_000,
};

const VARIABLES: Readonly<Record<keyof RenderLimits, string>> = {
  maxInFlight: 'LODB_RENDER_MAX_IN_FLIGHT',
  pinnedMaxInFlight: 'LODB_PINNED_RENDER_MAX_IN_FLIGHT',
  pinnedPerMinute: 'LODB_PINNED_RENDERS_PER_MINUTE',
  pinnedBurst: 'LODB_PINNED_RENDER_BURST',
  currentPerMinute: 'LODB_CURRENT_RENDERS_PER_MINUTE',
  currentBurst: 'LODB_CURRENT_RENDER_BURST',
  perClientMaxInFlight: 'LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT',
  pinnedPerClientPerMinute: 'LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE',
  currentPerClientPerMinute: 'LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE',
  currentPerClientBurst: 'LODB_CURRENT_RENDER_PER_CLIENT_BURST',
  trackedClients: 'LODB_RENDER_TRACKED_CLIENTS',
};

interface Share {
  readonly part: keyof RenderLimits;
  readonly whole: keyof RenderLimits;
  /** Whether the part must leave some of the whole to the others. */
  readonly isStrict: boolean;
}

// The pinned lane leaves room to the current pages, or a crawl of the older patches would
// take every slot again; a client's share is a share, never more than the whole.
const SHARES: readonly Share[] = [
  { part: 'pinnedMaxInFlight', whole: 'maxInFlight', isStrict: true },
  { part: 'perClientMaxInFlight', whole: 'maxInFlight', isStrict: false },
  { part: 'pinnedPerClientPerMinute', whole: 'pinnedPerMinute', isStrict: false },
  { part: 'currentPerClientPerMinute', whole: 'currentPerMinute', isStrict: false },
  { part: 'currentPerClientBurst', whole: 'currentBurst', isStrict: false },
];

// A client's share left to its default follows a whole set below it: lowering a lane's
// budget, the lever of an incident, must not stop the server. Only a share set above its
// whole is a mistake, which `checkShare` refuses.
function fitShare(limits: RenderLimits, env: Environment, share: Share): RenderLimits {
  const { part, whole, isStrict } = share;
  const value = env[VARIABLES[part]];
  if (isStrict || (value !== undefined && value !== '')) {
    return limits;
  }
  return { ...limits, [part]: Math.min(limits[part], limits[whole]) };
}

function checkShare(limits: RenderLimits, { part, whole, isStrict }: Share): void {
  const isCoherent = isStrict ? limits[part] < limits[whole] : limits[part] <= limits[whole];
  if (!isCoherent) {
    throw new Error(
      `${VARIABLES[part]} must stay ${isStrict ? 'below' : 'within'} ${VARIABLES[whole]},` +
        ` got ${limits[part]} for ${limits[whole]}.`,
    );
  }
}

/**
 * Reads the bounds of the render admission from the `LODB_*` variables of `VARIABLES`, each
 * optional (its default otherwise, a client's share capped by its whole), then checks that
 * each share fits in its whole.
 */
export function readRenderLimits(env: Environment): RenderLimits {
  const read = (limit: keyof RenderLimits) =>
    readPositiveInteger(env, VARIABLES[limit], DEFAULTS[limit]);
  const limits: RenderLimits = {
    maxInFlight: read('maxInFlight'),
    pinnedMaxInFlight: read('pinnedMaxInFlight'),
    pinnedPerMinute: read('pinnedPerMinute'),
    pinnedBurst: read('pinnedBurst'),
    currentPerMinute: read('currentPerMinute'),
    currentBurst: read('currentBurst'),
    perClientMaxInFlight: read('perClientMaxInFlight'),
    pinnedPerClientPerMinute: read('pinnedPerClientPerMinute'),
    currentPerClientPerMinute: read('currentPerClientPerMinute'),
    currentPerClientBurst: read('currentPerClientBurst'),
    trackedClients: read('trackedClients'),
  };
  const fitted = SHARES.reduce((current, share) => fitShare(current, env, share), limits);
  SHARES.forEach((share) => checkShare(fitted, share));
  return fitted;
}
