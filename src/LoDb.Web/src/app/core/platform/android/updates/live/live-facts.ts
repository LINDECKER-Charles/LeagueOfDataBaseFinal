import type { UpdateFacts } from '../plan/update-facts';

/** What the live update knows of the device, for the update decision. */
export type LiveFacts = Pick<
  UpdateFacts,
  'nextBundleId' | 'downloadedBundleIds' | 'rejectedBundleIds'
>;
