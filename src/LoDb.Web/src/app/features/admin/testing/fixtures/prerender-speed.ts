import type { PrerenderSpeedReport } from '../../../../core/api/generated/models/prerender-speed-report';

const SPEED: PrerenderSpeedReport = {
  generatedAt: '2026-10-21T21:30:00Z',
  isConfigured: true,
  isFixed: false,
  setting: { mode: 'adaptive', rendersPerMinute: 240, updatedAt: null },
  limits: {
    minRendersPerMinute: 30,
    maxRendersPerMinute: 240,
    manualMaxRendersPerMinute: 900,
    renderers: 2,
    rendererCpus: 1,
    brakeHostBusy: 0.9,
    brakeMinutes: 3,
    holdMinutes: 5,
    rendererCpuSecondsPerPage: 0.1,
    isCostMeasured: true,
    stackCpuSecondsPerPage: 0.03,
  },
  pace: null,
  resources: {
    at: '2026-10-21T21:29:40Z',
    hostCpus: 4,
    hostBusy: 0.4,
    hostSteal: 0.15,
    renderers: 2,
    renderersCores: 0.14,
    renderersPerMinute: 84,
    visitorsCores: 0.15,
    visitorsRefused: 0,
    olderRefusedPerMinute: 377,
    apiCores: 0.09,
  },
};

/**
 * The speed of the pre-rendering on prod as measured on 2026-10-21: two renderers at a core,
 * 0.1 s of CPU a page, an adaptive pass at 84 pages a minute on a VPS 40 % busy. `changes`
 * replaces fields of the report.
 */
export function prerenderSpeed(changes: Partial<PrerenderSpeedReport> = {}): PrerenderSpeedReport {
  return { ...SPEED, ...changes };
}
