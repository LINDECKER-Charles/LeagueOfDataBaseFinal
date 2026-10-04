/**
 * The countable facts of the About pages: the latest patch, the entries of each section on
 * it, and how many languages and patches the site serves. A number that could not be read is
 * `null`, never 0: unknown is not empty.
 */
export interface InventorySnapshot {
  readonly version: string | null;
  readonly champions: number | null;
  readonly items: number | null;
  readonly runes: number | null;
  readonly summoners: number | null;
  readonly languages: number | null;
  readonly versions: number | null;
}
