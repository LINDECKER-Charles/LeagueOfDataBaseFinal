import type { RenderLimits } from './render-limits';

type Environment = Readonly<Record<string, string | undefined>>;

// Sized for the production host (4 shared vCPU, one Node thread for the renderer): a render
// costs about 0.1 s of the renderer and as much of the API, which loads a whole catalog for
// each (version, language) it no longer holds. One pinned render a second stays a fraction
// of a core; a crawl of every patch in every language no longer takes the host.
const DEFAULTS: RenderLimits = {
  maxInFlight: 8,
  pinnedMaxInFlight: 3,
  pinnedPerMinute: 60,
  pinnedBurst: 10,
};

const VARIABLES: Readonly<Record<keyof RenderLimits, string>> = {
  maxInFlight: 'LODB_RENDER_MAX_IN_FLIGHT',
  pinnedMaxInFlight: 'LODB_PINNED_RENDER_MAX_IN_FLIGHT',
  pinnedPerMinute: 'LODB_PINNED_RENDERS_PER_MINUTE',
  pinnedBurst: 'LODB_PINNED_RENDER_BURST',
};

// A misconfigured variable stops the server at startup rather than serving on a guess.
function positiveIntegerOf(env: Environment, limit: keyof RenderLimits): number {
  const variable = VARIABLES[limit];
  const value = env[variable];
  if (value === undefined || value === '') {
    return DEFAULTS[limit];
  }
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 1) {
    throw new Error(`${variable} must be a positive integer, got "${value}".`);
  }
  return parsed;
}

/**
 * Reads the bounds of the render admission from `LODB_RENDER_MAX_IN_FLIGHT`,
 * `LODB_PINNED_RENDER_MAX_IN_FLIGHT`, `LODB_PINNED_RENDERS_PER_MINUTE` and
 * `LODB_PINNED_RENDER_BURST`, each optional. The pinned lane must leave room to the current
 * pages, or a crawl of the older patches would take every slot again.
 */
export function readRenderLimits(env: Environment): RenderLimits {
  const limits: RenderLimits = {
    maxInFlight: positiveIntegerOf(env, 'maxInFlight'),
    pinnedMaxInFlight: positiveIntegerOf(env, 'pinnedMaxInFlight'),
    pinnedPerMinute: positiveIntegerOf(env, 'pinnedPerMinute'),
    pinnedBurst: positiveIntegerOf(env, 'pinnedBurst'),
  };
  if (limits.pinnedMaxInFlight >= limits.maxInFlight) {
    throw new Error(
      `${VARIABLES.pinnedMaxInFlight} must stay below ${VARIABLES.maxInFlight}, got` +
        ` ${limits.pinnedMaxInFlight} for ${limits.maxInFlight}.`,
    );
  }
  return limits;
}
