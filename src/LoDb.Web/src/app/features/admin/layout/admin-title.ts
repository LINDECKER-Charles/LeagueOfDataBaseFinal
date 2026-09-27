import { effect, inject } from '@angular/core';
import type { Locale } from '../../../core/i18n/locales';
import { Seo } from '../../../core/seo/seo';

// The admin speaks French whatever the locale of the site.
const ADMIN_LOCALE: Locale = 'fr';

/**
 * Titles the document of an admin page "{title} · Admin · LODB", never indexed, each time
 * `title` changes. Its texts come from the catalogue: nothing is written before it arrived.
 */
export function injectAdminTitle(title: () => string): void {
  const seo = inject(Seo);
  effect(() => {
    const text = title();
    if (text !== '') {
      void seo.apply({ kind: 'private', titleFormat: 'admin', locale: ADMIN_LOCALE, title: text });
    }
  });
}
