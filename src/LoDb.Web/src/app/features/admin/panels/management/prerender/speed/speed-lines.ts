import type { PrerenderSpeedReport } from '../../../../../../core/api/generated/models/prerender-speed-report';
import { duration } from '../../../../format/duration';
import { figure } from '../../../../format/figure';
import type { AdminText } from '../../../../shared/inject-admin-text';
import type { SpeedFigures } from './math/speed-figures';

/** One line of the speed block: what it measures, and its value; `warn` stands out. */
export interface SpeedLine {
  readonly key: string;
  readonly label: string;
  readonly value: string;
  readonly isWarning: boolean;
}

/** The lines of the block: the resources now, and the forecast of the pace looked at. */
export interface SpeedLines {
  /** Null before the API's first read of the host. */
  readonly now: readonly SpeedLine[] | null;
  readonly forecast: readonly SpeedLine[];
}

const NOW = 'prerender.speed.now';
const FORECAST = 'prerender.speed.forecast';
const SECONDS_PER_MINUTE = 60;
const CENTS = 100;
const DASH = '—';

function cores(value: number | null | undefined): string {
  return value === null || value === undefined ? DASH : value.toFixed(2);
}

function share(value: number | null | undefined): string {
  return value === null || value === undefined ? DASH : figure(value, 'share');
}

/** A line to write: its key, the values of its text, whether it warns. */
interface LineSpec {
  readonly key: string;
  readonly params: Readonly<Record<string, unknown>>;
  readonly isWarning?: boolean;
}

function line(texts: AdminText, { key, params, isWarning = false }: LineSpec): SpeedLine {
  return { key, label: texts.text(key), value: texts.text(`${key}_value`, params), isWarning };
}

function freeCores(busy: number | null | undefined, cpus: number | null | undefined) {
  return cpus && busy !== null && busy !== undefined ? (1 - busy) * cpus : null;
}

type Readings = NonNullable<PrerenderSpeedReport['resources']>;

function nowSpecs(read: Readings): LineSpec[] {
  const host = {
    busy: share(read.hostBusy),
    steal: share(read.hostSteal),
    free: cores(freeCores(read.hostBusy, read.hostCpus)),
    cpus: read.hostCpus ?? DASH,
  };
  const renderers = {
    count: read.renderers,
    cores: cores(read.renderersCores),
    pace: Math.round(read.renderersPerMinute ?? 0),
  };
  const visitors = {
    cores: cores(read.visitorsCores),
    refused: read.visitorsRefused ?? DASH,
    older: Math.round(read.olderRefusedPerMinute ?? 0),
  };
  const isRefused = (read.visitorsRefused ?? 0) > 0;
  return [
    { key: `${NOW}.host`, params: host },
    { key: `${NOW}.renderers`, params: renderers },
    { key: `${NOW}.visitors`, params: visitors, isWarning: isRefused },
    { key: `${NOW}.api`, params: { cores: cores(read.apiCores) } },
  ];
}

function eta(minutes: number | null): string {
  return minutes === null ? DASH : duration(minutes * SECONDS_PER_MINUTE);
}

// What the pace takes of the VPS, and what it leaves.
function cpuSpecs(figures: SpeedFigures): LineSpec[] {
  const cpu = figures.cpu;
  const parts = {
    total: cores(cpu.total),
    renderers: cores(cpu.renderers),
    stack: cores(cpu.stack),
  };
  const after = { busy: share(cpu.hostBusy), margin: cores(cpu.margin) };
  return [
    { key: `${FORECAST}.cpu`, params: parts },
    { key: `${FORECAST}.host`, params: after, isWarning: cpu.isNearBrake },
  ];
}

// The renderers' best, the ends of the pass and of the catalogue, and the cost.
function planSpecs(speed: PrerenderSpeedReport, figures: SpeedFigures, measured: string) {
  const limits = speed.limits;
  const capacity = {
    capacity: figure(figures.capacity, 'int'),
    renderers: limits.renderers,
    cpus: limits.rendererCpus,
    cost: limits.rendererCpuSecondsPerPage,
    measured,
  };
  const euros =
    figures.storageEuros === null ? DASH : figure(figures.storageEuros * CENTS, 'euros');
  const pass = { eta: eta(figures.passMinutes), pages: figure(figures.passPagesLeft ?? 0, 'int') };
  const catalogue = {
    eta: eta(figures.catalogueMinutes),
    pages: figure(figures.cataloguePages ?? 0, 'compact'),
  };
  return [
    { key: `${FORECAST}.capacity`, params: capacity, isWarning: figures.isOverCapacity },
    { key: `${FORECAST}.pass`, params: pass },
    { key: `${FORECAST}.catalogue`, params: catalogue },
    { key: `${FORECAST}.cost`, params: { euros, share: share(figures.cpu.vpsShare) } },
  ];
}

/**
 * The lines of the speed block (§ « Vitesse réglable »): the VPS, the renderers, the
 * visitors' SSR and the API as last read; then, at the pace looked at, the CPU it takes, the
 * VPS after it and its margin, the renderers' best, when the active pass and the catalogue
 * end, and what it costs. A line that warns (near the brake, past the renderers' best, a
 * visitor refused) stands out.
 */
export function speedLines(
  speed: PrerenderSpeedReport,
  figures: SpeedFigures,
  texts: AdminText,
): SpeedLines {
  const write = (spec: LineSpec) => line(texts, spec);
  const cost = speed.limits.isCostMeasured ? 'measured' : 'estimated';
  const measured = texts.text(`${FORECAST}.${cost}`);
  const read = speed.resources;
  return {
    now: read ? nowSpecs(read).map(write) : null,
    forecast: [...cpuSpecs(figures), ...planSpecs(speed, figures, measured)].map(write),
  };
}
