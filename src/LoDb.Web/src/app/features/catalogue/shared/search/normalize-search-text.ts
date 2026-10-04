const COMBINING_MARKS = /[\u0300-\u036f]/g;

/**
 * How a reader's typing compares to a name: lowercased, accents stripped, so `feerique` finds
 * `Féérique`. Punctuation stays: apostrophes belong to champion names (`Kai'Sa`).
 */
export function normalizeSearchText(value: string): string {
  return value.normalize('NFD').replace(COMBINING_MARKS, '').toLowerCase();
}
