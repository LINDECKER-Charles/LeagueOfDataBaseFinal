import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from '../../../core/i18n/locales';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { Seo } from '../../../core/seo/seo';
import type { SeoPage } from '../../../core/seo/seo-page';

type Translate = (key: string) => string;

/**
 * Writes the head of an editorial page once the catalogues its texts come from are loaded
 * (the root one, then each scope named), and again when the router reuses the page for
 * another locale. Scoped keys carry their scope as prefix (`about.faq.title`). Call it from
 * the page's constructor: the prerender waits for the head.
 */
export function applyEditorialHead(
  scopes: readonly string[],
  build: (translate: Translate, locale: Locale) => SeoPage,
): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const locale = page.locale();
    tasks.run(async () => {
      const catalogues = [locale, ...scopes.map((scope) => `${scope}/${locale}`)];
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        catalogues.map((path) => firstValueFrom(transloco.load(path)).catch(() => undefined)),
      );
      const translate: Translate = (key) => transloco.translate(key, {}, locale);
      await seo.apply(build(translate, locale));
    });
  });
}
