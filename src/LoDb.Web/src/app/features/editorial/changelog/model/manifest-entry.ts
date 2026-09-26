/**
 * One line of `changelog/manifest.json`, the index of the published releases, newest first.
 * The page only needs where a release lives and which version it ships.
 */
export interface ManifestEntry {
  /** Name of the release file, `changelog/<id>.json`. */
  readonly id: string;
  /** The version the release ships; `null` when the manifest leaves it out. */
  readonly version: string | null;
}
