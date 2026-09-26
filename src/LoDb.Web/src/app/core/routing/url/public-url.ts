import type { Locale } from '../../i18n/locales';
import type { QueryString } from './query-string';

/**
 * A site URL cut along the grammar of ADR 0005: `/{locale}/[{version}/]{page…}?{query}#…`.
 */
export interface PublicUrl {
  /** Locale prefix, or null for the pages outside a locale (`/b/…`, `/admin`). */
  readonly locale: Locale | null;
  /** Version segment right after the locale, or null when the page follows the latest. */
  readonly version: string | null;
  /** Segments of the page below the locale and the version, as the URL encodes them. */
  readonly page: readonly string[];
  readonly query: QueryString;
  /** Fragment with its `#`, or ''. */
  readonly fragment: string;
  /** Whether the path ends with a slash, as the home page `/fr/` does. */
  readonly trailingSlash: boolean;
}
