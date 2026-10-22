import type { PatchHistory } from '../data/patch-history';

/**
 * Whether a version has a page of changes: one from the oldest that has one up to the latest,
 * in the order Data Dragon lists them (newest first). None before the first promotion, nor for
 * a version listed after the latest one.
 */
export function hasChangesPage(version: string, history: PatchHistory): boolean {
  const { latest, versions, oldestChangesVersion } = history;
  if (latest === null || oldestChangesVersion === null) {
    return false;
  }
  const index = versions.indexOf(version);
  return index >= versions.indexOf(latest) && index <= versions.indexOf(oldestChangesVersion);
}
