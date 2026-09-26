import type { Locale } from '../../i18n/locales';
import type { SeoPage } from '../seo-page';
import type { SeoTexts } from './seo-texts';

/** A page and what the service resolved around it. */
export interface HeadInput {
  readonly page: SeoPage;
  readonly texts: SeoTexts;
  /** Canonical origin, without a trailing slash. */
  readonly origin: string;
  /** The page's locale, resolved. */
  readonly locale: Locale;
}
