// The block of the combining diacritical marks, which NFD splits off accented letters.
const FIRST_COMBINING_MARK = 0x300;
const LAST_COMBINING_MARK = 0x36f;

function isCombiningMark(char: string): boolean {
  const code = char.codePointAt(0) ?? 0;
  return code >= FIRST_COMBINING_MARK && code <= LAST_COMBINING_MARK;
}

// Lowercased, accents stripped, so `seraphine` finds `Séraphine`. The account pickers and the
// catalogue lists compare alike; a feature cannot share their copy.
function normalized(value: string): string {
  const letters = [...value.normalize('NFD')].filter((char) => !isCombiningMark(char));
  return letters.join('').toLowerCase();
}

interface Entry<T> {
  readonly value: T;
  readonly text: string;
}

/**
 * Values searched by what a visitor types, whatever the case and the accents. The text of
 * each is normalized once, so typing only runs the comparisons: the pickers join the id to
 * the name, so `Kaisa` finds Kai'Sa and namesake items (Arena variants) tell apart.
 */
export class SearchIndex<T> {
  private readonly entries: readonly Entry<T>[];

  constructor(values: readonly T[], textOf: (value: T) => string) {
    this.entries = values.map((value) => ({ value, text: normalized(textOf(value)) }));
  }

  /** The values the query finds, in their order, among those `keep` accepts. */
  matching(query: string, keep: (value: T) => boolean = () => true): T[] {
    const needle = normalized(query.trim());
    return this.entries
      .filter((entry) => keep(entry.value) && entry.text.includes(needle))
      .map((entry) => entry.value);
  }
}
