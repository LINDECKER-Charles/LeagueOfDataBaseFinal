import type { ConstellationRune } from './constellation-rune';

/** A minor row of a rune path. */
export interface ConstellationRow {
  /** The API's name of the row, such as `row1`. */
  readonly slot: string;
  /** Its number under the keystones, from 1. */
  readonly number: number;
  readonly runes: readonly ConstellationRune[];
}
