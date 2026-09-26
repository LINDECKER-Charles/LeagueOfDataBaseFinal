import { PendingTasks, type Signal, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Seo } from '../../../../core/seo/seo';
import type { TrendsView } from '../loading/trends-view';
import type { TrendsTexts } from './trends-texts';
import { trendsSeoPage } from './trends-seo-page';

/** The Transloco scope of the SEO texts (`public/i18n/seo/`). */
const SEO_SCOPE = 'seo';

/**
 * Writes the head of the trends once the root catalogue and the `seo` scope of its locale are
 * loaded, and again for each page or filter. Called in the page's constructor: the server
 * render waits for the head.
 */
export function applyTrendsHead(view: Signal<TrendsView>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  effect(() => {
    const { locale, page } = view();
    tasks.run(async () => {
      const scoped = `${SEO_SCOPE}/${locale}`;
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        [locale, scoped].map((path) => firstValueFrom(transloco.load(path)).catch(() => null)),
      );
      const texts: TrendsTexts = {
        seo: (key) => transloco.translate(key, {}, scoped),
        main: (key) => transloco.translate(key, {}, locale),
      };
      await seo.apply(trendsSeoPage(page.rows, texts, locale));
    });
  });
}
