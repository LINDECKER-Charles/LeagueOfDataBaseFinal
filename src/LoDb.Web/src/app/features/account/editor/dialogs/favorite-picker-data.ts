import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import type { PickerCatalog } from '../picker/picker-catalog';

/** What the favorite picker is opened with; it closes on an entry, or null to empty. */
export interface FavoritePickerData {
  readonly slot: FavoriteSlot;
  /** The editor's lists: the dialog lives out of the editor's injector. */
  readonly catalog: PickerCatalog;
  /** The id in the slot now, marked in the list and removable; null for an empty slot. */
  readonly current: string | null;
}
