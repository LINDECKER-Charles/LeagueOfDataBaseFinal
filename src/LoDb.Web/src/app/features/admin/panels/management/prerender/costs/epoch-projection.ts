import type { PrerenderStorageView } from '../../../../../../core/api/generated/models/prerender-storage-view';

/** What a new epoch would add to the store while both coexist. */
export interface EpochProjection {
  readonly bytes: number;
  /** Its monthly cost, in the currency of the storage. */
  readonly monthlyCost: number;
}

const SERVED = 'served';

/**
 * What a new epoch would weigh and cost a month (§ 5.3), taken as the size of the served
 * one: a new epoch renders the same pages. Null while the served epoch holds nothing, as
 * nothing then says what a whole epoch weighs.
 */
export function epochProjection(storage: PrerenderStorageView): EpochProjection | null {
  const served = storage.epochs.find((epoch) => epoch.state === SERVED);
  if (!served || served.bytes <= 0) {
    return null;
  }
  return { bytes: served.bytes, monthlyCost: served.monthlyCost };
}
