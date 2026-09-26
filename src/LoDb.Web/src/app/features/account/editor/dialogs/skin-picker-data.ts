import type { PickerCatalog } from '../picker/picker-catalog';

/** What the skin picker is opened with; it closes on a skin, or null to remove the banner. */
export interface SkinPickerData {
  /** The editor's lists: the dialog lives out of the editor's injector. */
  readonly catalog: PickerCatalog;
  /** The id of the banner now, marked among the skins and removable; null for none. */
  readonly current: string | null;
}
