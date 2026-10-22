// What `/api/meta` says of the pages of changes; the patch history passes its own copy.
interface ChangesWindow {
  /** Every version Data Dragon lists, newest first. */
  readonly versions: readonly string[];
  readonly oldestChangesVersion: string | null;
  /** The end of the index's coverage: the latest version, but while the index lags. */
  readonly newestChangesVersion: string | null;
}

/**
 * Whether a version has a page of changes: one from the oldest that has one up to the newest,
 * in the order Data Dragon lists them (newest first). None before the first promotion, nor for
 * a version the index of states does not cover yet (one listed after the latest, the latest
 * while the index lags behind its promotion): any other address answers a 404. A newest
 * version the list lacks is newer than all of it: the API's copy of the list, cached, can miss
 * the patch just promoted, which the index already covers.
 */
export function hasChangesPage(version: string, meta: ChangesWindow): boolean {
  const { versions, oldestChangesVersion, newestChangesVersion } = meta;
  if (oldestChangesVersion === null || newestChangesVersion === null) {
    return false;
  }
  if (version === newestChangesVersion) {
    return true;
  }
  const newest = Math.max(versions.indexOf(newestChangesVersion), 0);
  const index = versions.indexOf(version);
  return index >= newest && index <= versions.indexOf(oldestChangesVersion);
}
