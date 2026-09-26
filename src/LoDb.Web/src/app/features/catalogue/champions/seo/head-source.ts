import type { Locale } from '../../../../core/i18n/locales';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { Translate } from './translate';

/** What a champions page writes its head from, once its data has arrived. */
export interface HeadSource {
  readonly locale: Locale;
  readonly build: (translate: Translate) => SeoPage;
}
