import { normalizeSearchText } from './normalize-search-text';
import type { PickerEntry } from './picker-entry';

interface Matches {
  readonly entries: ReadonlySet<PickerEntry>;
  /** Paths whose header matched: all of their runes stay. */
  readonly headers: ReadonlySet<string>;
  /** Paths with a matching rune: their header stays. */
  readonly parents: ReadonlySet<string>;
}

function matchesOf(entries: readonly PickerEntry[], query: string): Matches {
  const matched = entries.filter((entry) => entry.searchText.includes(query));
  const headers = new Set<string>();
  const parents = new Set<string>();
  for (const entry of matched) {
    if (entry.isGroup) {
      headers.add(entry.id);
    } else if (entry.groupId !== undefined) {
      parents.add(entry.groupId);
    }
  }
  return { entries: new Set(matched), headers, parents };
}

function isShown(entry: PickerEntry, matches: Matches): boolean {
  if (matches.entries.has(entry)) {
    return true;
  }
  if (entry.isGroup) {
    return matches.parents.has(entry.id);
  }
  return entry.groupId !== undefined && matches.headers.has(entry.groupId);
}

/**
 * The entries a query keeps, in their order, whatever the case and the accents. The rune
 * list stays readable: a path's header stays with any of its matching runes, and a matching
 * header keeps all of its runes.
 */
export function filterOptions<T extends PickerEntry>(entries: readonly T[], query: string): T[] {
  const normalized = normalizeSearchText(query.trim());
  if (normalized === '') {
    return [...entries];
  }
  const matches = matchesOf(entries, normalized);
  return entries.filter((entry) => isShown(entry, matches));
}
