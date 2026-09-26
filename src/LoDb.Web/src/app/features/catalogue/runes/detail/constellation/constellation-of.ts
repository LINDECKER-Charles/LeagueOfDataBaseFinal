import type { RuneEntry } from '../../../../../core/api/generated/models/rune-entry';
import type { RuneRow } from '../../../../../core/api/generated/models/rune-row';
import type { RuneTreeDetails } from '../../../../../core/api/generated/models/rune-tree-details';
import { rowNumberOf } from '../../paths/row-number-of';
import type { Constellation } from './constellation';
import type { ConstellationRow } from './constellation-row';
import type { ConstellationRune } from './constellation-rune';

// A keystone is read in full; a minor rune by its summary, its long text on demand.
function runeOf(entry: RuneEntry, isKeystone: boolean): ConstellationRune {
  const [short, long] = [entry.shortDesc, entry.longDesc];
  const hasMore = !isKeystone && short !== '' && long !== '' && long !== short;
  return {
    id: entry.id,
    key: entry.key,
    name: entry.name,
    image: entry.image,
    anchor: `rune-${entry.key}`,
    summary: isKeystone ? long || short : short || long,
    details: hasMore ? long : null,
  };
}

function rowOf(row: RuneRow): ConstellationRow | null {
  const number = rowNumberOf(row.slot);
  if (number === null) {
    return null;
  }
  return { slot: row.slot, number, runes: row.runes.map((entry) => runeOf(entry, false)) };
}

/**
 * The constellation of a rune path page: the keystones on top, then the minor rows by their
 * number, whatever order the API lists them in. A path without runes has none.
 */
export function constellationOf(tree: RuneTreeDetails): Constellation | null {
  const keystones = tree.slots
    .filter((row) => rowNumberOf(row.slot) === null)
    .flatMap((row) => row.runes.map((entry) => runeOf(entry, true)));
  const rows = tree.slots
    .map(rowOf)
    .filter((row): row is ConstellationRow => row !== null && row.runes.length > 0)
    .sort((a, b) => a.number - b.number);
  return keystones.length + rows.length > 0 ? { keystones, rows } : null;
}
