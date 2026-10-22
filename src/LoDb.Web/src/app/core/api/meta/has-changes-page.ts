// What `/api/meta` says of the pages of changes; the patch history passes its own copy.
interface ChangesWindow {
  readonly latest?: string | null;
  /** Every version Data Dragon lists, newest first. */
  readonly versions: readonly string[];
  readonly oldestChangesVersion: string | null;
}

/**
 * Whether a version has a page of changes: one from the oldest that has one up to the latest,
 * in the order Data Dragon lists them (newest first). None before the first promotion, nor for
 * a version listed after the latest one: any other address answers a 404.
 */
export function hasChangesPage(version: string, meta: ChangesWindow): boolean {
  const { latest, versions, oldestChangesVersion } = meta;
  if (!latest || oldestChangesVersion === null) {
    return false;
  }
  const index = versions.indexOf(version);
  return index >= versions.indexOf(latest) && index <= versions.indexOf(oldestChangesVersion);
}
