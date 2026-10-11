import type { Locale } from '../../../core/i18n/locales';

/** What the patch history resolves: its locale and the versions `/api/meta` lists. */
export interface PatchHistory {
  readonly locale: Locale;
  /** The version the short URLs show; null until the first ingestion completes. */
  readonly latest: string | null;
  /** Every version Data Dragon lists, newest first, the long tail included. */
  readonly versions: readonly string[];
}
