import type { ItemDetails } from '../api/generated/models/item-details';

/** The fields of the index's rule a detail of every resource carries. */
export type DetailStateFields = Pick<
  ItemDetails,
  'canonical' | 'state' | 'history' | 'next' | 'isPresentInLatest' | 'changes'
>;
