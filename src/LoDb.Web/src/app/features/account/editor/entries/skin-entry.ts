import type { PickerEntry } from './picker-entry';

/** A skin of the banner picker: a row that carries its banner art. */
export interface SkinEntry extends PickerEntry {
  /** The centered splash the profile banner shows. */
  readonly banner: string;
}
