import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { breadcrumbList } from '../../core/seo/json-ld/site/breadcrumb-list';
import { Seo } from '../../core/seo/seo';
import type { SeoPage } from '../../core/seo/seo-page';

// The API texts (its breadcrumb name), then the page titles and descriptions.
const SCOPES = ['api', 'seo'];

type Translate = (key: string) => string;

function developersHead(translate: Translate): SeoPage {
  return {
    title: translate('seo.developers.title'),
    description: translate('seo.developers.description'),
    path: 'developers',
    jsonLd: (urls) => [
      breadcrumbList([
        { name: translate('header.navigation.home'), url: urls.page('') },
        { name: translate('api.nav.developers'), url: urls.canonical },
      ]),
    ],
  };
}

/**
 * Writes the indexable head of `/{locale}/developers` once its catalogues are loaded (the
 * root one, `api` and `seo`), and again when the router reuses the page for another locale.
 * Called from the page's constructor: the server render waits for the head.
 */
export function applyDevelopersHead(): void {
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
      await seo.apply(developersHead((key) => transloco.translate(key, {}, locale)));
    });
  });
}
