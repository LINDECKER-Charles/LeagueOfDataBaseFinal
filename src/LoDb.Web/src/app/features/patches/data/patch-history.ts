import type { Locale } from '../../../core/i18n/locales';

/** What the patch history resolves: its locale and the versions `/api/meta` lists. */
export interface PatchHistory {
  readonly locale: Locale;
  /** The version the short URLs show; null until the first ingestion completes. */
  readonly latest: string | null;
  /** Every version Data Dragon lists, newest first, the long tail included. */
  readonly versions: readonly string[];
  /**
   * The oldest version that has a page of changes, every version from it up to the latest
   * one having one; null while the index of the entities' states covers fewer than two.
   */
  readonly oldestChangesVersion: string | null;
}
