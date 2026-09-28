import type { ConstellationRow } from './constellation-row';
import type { ConstellationRune } from './constellation-rune';

/** A rune path strung as a constellation: its keystones, then its minor rows in order. */
export interface Constellation {
  readonly keystones: readonly ConstellationRune[];
  readonly rows: readonly ConstellationRow[];
}
