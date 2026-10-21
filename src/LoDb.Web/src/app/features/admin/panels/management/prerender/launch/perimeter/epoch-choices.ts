import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';

/** What a pass may write: the served epoch, a standby one, or a new one. */
export type EpochKind = 'served' | 'standby' | 'new';

/** One card of « Epoch écrite » (§ 3.4). */
export interface EpochChoice {
  readonly kind: EpochKind;
  readonly epoch: number;
  /** Standby: the share of its (older patch, locale) complete, 0 to 1; null when unknown. */
  readonly share: number | null;
  /** New: what it costs a month once complete, as much as the served one; 0 otherwise. */
  readonly monthlyCost: number;
  readonly currency: string;
}

const STANDBY = 'standby';

function shareOf(report: PrerenderReport, epoch: number): number | null {
  const coverage = report.storage.epochs.find((volume) => volume.epoch === epoch)?.coverage;
  return coverage && coverage.expectedUnits > 0
    ? coverage.completeUnits / coverage.expectedUnits
    : null;
}

/**
 * The epochs a pass may write, from the report (§ 3.4): the served one, each standby one,
 * then a new one numbered past every epoch of the store, the served one included. Never the
 * number of a retired epoch, whose lifecycle rule `retire-pages-<N>` would delete the pass.
 */
export function epochChoices(report: PrerenderReport): EpochChoice[] {
  const { storage, servedEpoch } = report;
  const choice = (kind: EpochKind, epoch: number, monthlyCost = 0): EpochChoice => ({
    kind,
    epoch,
    share: kind === STANDBY ? shareOf(report, epoch) : null,
    monthlyCost,
    currency: storage.currency,
  });
  const standby = storage.epochs
    .filter((volume) => volume.state === STANDBY && volume.epoch !== servedEpoch)
    .map((volume) => choice('standby', volume.epoch));
  const newest = Math.max(servedEpoch, ...storage.epochs.map((volume) => volume.epoch));
  const served = storage.epochs.find((volume) => volume.epoch === servedEpoch);
  return [
    choice('served', servedEpoch),
    ...standby,
    choice('new', newest + 1, served?.monthlyCost ?? 0),
  ];
}
