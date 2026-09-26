import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { Seo } from '../../core/seo/seo';
import type { SeoPage } from '../../core/seo/seo-page';

type Translate = (key: string) => string;

// The donation texts, then the page titles and descriptions.
const SCOPES = ['donate', 'seo'];

/**
 * Writes the head of a donation page once its catalogues are loaded (the root one, `donate`
 * and `seo`), and again when the router reuses the page for another locale. Scoped keys
 * carry their scope as prefix (`donate.success.title`). Call it from the page's constructor:
 * the server render waits for the head.
 */
export function applyDonateHead(build: (translate: Translate) => SeoPage): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const locale = page.locale();
    tasks.run(async () => {
      const catalogues = [locale, ...SCOPES.map((scope) => `${scope}/${locale}`)];
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        catalogues.map((path) => firstValueFrom(transloco.load(path)).catch(() => undefined)),
      );
      const translate: Translate = (key) => transloco.translate(key, {}, locale);
      await seo.apply(build(translate));
    });
  });
}
