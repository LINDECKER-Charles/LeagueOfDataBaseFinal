const COMBINING_MARKS = /[\u0300-\u036f]/g;

/**
 * How a visitor's typing compares to a name: lowercased, accents stripped, so `seraphine`
 * finds `Séraphine`. The catalogue lists compare alike; a feature cannot share their copy.
 */
export function normalizeSearchText(value: string): string {
  return value.normalize('NFD').replace(COMBINING_MARKS, '').toLowerCase();
}
