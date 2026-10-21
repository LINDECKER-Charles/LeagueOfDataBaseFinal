import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import type { PrerenderSpeedReport } from '../../../../../../../core/api/generated/models/prerender-speed-report';

/** What the figures beside the speed control are computed from. */
export interface SpeedInput {
  /** The pace the operator looks at, in pages a minute (the one set, or the one typed). */
  readonly pace: number;
  readonly speed: PrerenderSpeedReport;
  /** The pre-rendering's report: the active pass, the served epoch, the prices. */
  readonly report: PrerenderReport;
}

/** The CPU a pace takes and what it leaves of the VPS, in cores; null while unknown. */
export interface CpuForecast {
  /** The renderers' CPU at that pace. */
  readonly renderers: number;
  /** The API's and Postgres's CPU at that pace. */
  readonly stack: number;
  /** Both. */
  readonly total: number;
  /** The VPS's busy share once at that pace (0..1, steal counted); null while unread. */
  readonly hostBusy: number | null;
  /** Cores left free once at that pace; null while unread. */
  readonly margin: number | null;
  /** The share of the VPS the pass takes (0..1); null while its CPUs are unknown. */
  readonly vpsShare: number | null;
  /** Whether the VPS would reach the brake's threshold at that pace. */
  readonly isNearBrake: boolean;
}

/** Everything the speed block shows of a pace. */
export interface SpeedFigures {
  readonly cpu: CpuForecast;
  /** The renderers' best, in pages a minute: past it, the pace is not reached. */
  readonly capacity: number;
  readonly isOverCapacity: boolean;
  /** Minutes until the active pass ends at that pace; null without one. */
  readonly passMinutes: number | null;
  readonly passPagesLeft: number | null;
  /** Pages of the older patches the served epoch still lacks, estimated. */
  readonly cataloguePages: number | null;
  readonly catalogueMinutes: number | null;
  /** What those pages add to the store's bill each month, in euros. */
  readonly storageEuros: number | null;
}

const SECONDS_PER_MINUTE = 60;
const BYTES_PER_GB = 1e9;
const SERVED = 'served';
// Measured in production on 2026-10-21 (16.14.1 in English): a unit of 901 pages of about
// 35 KB, the figures until the served epoch holds pages of its own.
const PAGES_PER_UNIT = 900;
const BYTES_PER_PAGE = 35_000;

// The CPU the pass takes now, renderers and stack: a forecast replaces it, never adds to it.
function runningCores(speed: PrerenderSpeedReport): number {
  const resources = speed.resources;
  const pace = resources?.renderersPerMinute ?? 0;
  const stack = (pace * speed.limits.stackCpuSecondsPerPage) / SECONDS_PER_MINUTE;
  return (resources?.renderersCores ?? 0) + stack;
}

function cpuOf(pace: number, speed: PrerenderSpeedReport): CpuForecast {
  const { limits, resources } = speed;
  const renderers = (pace * limits.rendererCpuSecondsPerPage) / SECONDS_PER_MINUTE;
  const stack = (pace * limits.stackCpuSecondsPerPage) / SECONDS_PER_MINUTE;
  const total = renderers + stack;
  const cpus = resources?.hostCpus ?? 0;
  const busy = resources?.hostBusy ?? null;
  const unread = { hostBusy: null, margin: null, vpsShare: null, isNearBrake: false };
  if (cpus <= 0 || busy === null) {
    return { renderers, stack, total, ...unread };
  }
  const hostBusy = Math.min(1, Math.max(0, busy + (total - runningCores(speed)) / cpus));
  return {
    renderers,
    stack,
    total,
    hostBusy,
    margin: (1 - hostBusy) * cpus,
    vpsShare: total / cpus,
    isNearBrake: hostBusy >= limits.brakeHostBusy,
  };
}

function minutesFor(pages: number | null, pace: number): number | null {
  return pages === null || pace <= 0 ? null : Math.max(0, pages) / pace;
}

function catalogueOf(report: PrerenderReport): { pages: number; bytesPerPage: number } | null {
  const served = report.storage.epochs.find((epoch) => epoch.state === SERVED);
  const coverage = served?.coverage;
  if (!coverage) {
    return null;
  }
  const complete = coverage.completeUnits;
  const perUnit = complete > 0 && served.pages > 0 ? served.pages / complete : PAGES_PER_UNIT;
  const perPage = served.pages > 0 ? served.bytes / served.pages : BYTES_PER_PAGE;
  return { pages: Math.round(coverage.gapUnits * perUnit), bytesPerPage: perPage };
}

/**
 * The figures of a pace (§ « Vitesse réglable »): the CPU it takes, what it leaves the VPS
 * and whether it nears the brake, the renderers' best, when the active pass and the whole
 * catalogue would end at it, and what the pages still to render add to the store's bill. The
 * VPS itself costs nothing more: the pass only takes a share of its CPU.
 */
export function speedFigures({ pace, speed, report }: SpeedInput): SpeedFigures {
  const { limits } = speed;
  const capacity = Math.floor(
    (limits.renderers * limits.rendererCpus * SECONDS_PER_MINUTE) /
      limits.rendererCpuSecondsPerPage,
  );
  const passPagesLeft = report.current?.pagesLeft ?? null;
  const catalogue = catalogueOf(report);
  const price = report.storage.pricePerGbMonthOneZone;
  return {
    cpu: cpuOf(pace, speed),
    capacity,
    isOverCapacity: pace > capacity,
    passMinutes: minutesFor(passPagesLeft, Math.min(pace, capacity)),
    passPagesLeft,
    cataloguePages: catalogue?.pages ?? null,
    catalogueMinutes: minutesFor(catalogue?.pages ?? null, Math.min(pace, capacity)),
    storageEuros: catalogue
      ? (catalogue.pages * catalogue.bytesPerPage * price) / BYTES_PER_GB
      : null,
  };
}
