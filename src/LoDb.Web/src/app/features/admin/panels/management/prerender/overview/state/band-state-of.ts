import type { PrerenderEpochCoverage } from '../../../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderEpochVolume } from '../../../../../../../core/api/generated/models/prerender-epoch-volume';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';

/** The band of an epoch to serve next: ready, or nearly so. */
export type EpochBand =
  | {
      readonly kind: 'ready';
      readonly epoch: number;
      /** The older patches, each held in every locale or failed for good. */
      readonly versions: number;
      /** (version, locale) failed for good, which the SSR keeps serving. */
      readonly failedUnits: number;
      /** Pages failed in the units held, which the SSR keeps serving. */
      readonly failedPages: number;
    }
  | {
      readonly kind: 'nearly';
      readonly epoch: number;
      /** (expected − gaps) / expected, from 0 to 1. */
      readonly share: number;
      /** (version, locale) with a gap. */
      readonly missing: number;
    };

/** The bands above the monitor (§ 2.4). */
export interface BandState {
  /** The host is not configured: no pass can start. */
  readonly isDormant: boolean;
  /** One band of epoch at most: a ready one wins over one nearly ready. */
  readonly epoch: EpochBand | null;
}

const STANDBY = 'standby';
// An epoch this close to whole is worth completing rather than written anew.
const NEARLY = 0.9;

// The older patches are the same whatever the epoch: any coverage read counts them.
function pinnedOf(report: PrerenderReport): number {
  const coverage = report.storage.epochs.find((epoch) => epoch.coverage)?.coverage;
  return coverage?.pinnedVersions ?? 0;
}

function readyOf(report: PrerenderReport, epoch: number): EpochBand {
  const coverage = report.storage.epochs.find((volume) => volume.epoch === epoch)?.coverage;
  return {
    kind: 'ready',
    epoch,
    versions: coverage?.pinnedVersions ?? pinnedOf(report),
    failedUnits: coverage?.failedUnits ?? 0,
    failedPages: coverage?.failedPages ?? 0,
  };
}

function nearShare(coverage: PrerenderEpochCoverage): number {
  return (coverage.expectedUnits - coverage.gapUnits) / coverage.expectedUnits;
}

// A standby epoch no pass writes, with gaps, at 90 % at least: the newest such one.
function isNearly(volume: PrerenderEpochVolume): boolean {
  const coverage = volume.coverage;
  if (volume.state !== STANDBY || volume.isWriting || !coverage) {
    return false;
  }
  return coverage.gapUnits > 0 && coverage.expectedUnits > 0 && nearShare(coverage) >= NEARLY;
}

function nearlyOf(report: PrerenderReport): EpochBand | null {
  const volume = report.storage.epochs.find(isNearly);
  if (!volume?.coverage) {
    return null;
  }
  const { coverage } = volume;
  return {
    kind: 'nearly',
    epoch: volume.epoch,
    share: nearShare(coverage),
    missing: coverage.gapUnits,
  };
}

/**
 * The bands of the page from its report: the host not configured; an epoch ready to serve
 * (no gap left, its failures for good and failed pages named, § 6.5), or else a standby epoch
 * nearly ready, worth completing. The API judges readiness; the page only words it.
 */
export function bandStateOf(report: PrerenderReport): BandState {
  const ready = report.readyEpoch;
  return {
    isDormant: !report.isConfigured,
    epoch: ready === null || ready === undefined ? nearlyOf(report) : readyOf(report, ready),
  };
}
