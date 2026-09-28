import type { Locale } from '../../i18n/locales';

/** Where a page lives, in the grammar of ADR 0005: `/{locale}/[{version}/]{path}`. */
export interface PageAddress {
  /** Canonical origin, without a trailing slash. */
  readonly origin: string;
  readonly locale: Locale;
  /** A pinned past version; null for the latest, whose URLs carry no version. */
  readonly version: string | null;
  /** Path below the locale and version, such as `items/1004-faerie-charm`; `''` for home. */
  readonly path: string;
}
