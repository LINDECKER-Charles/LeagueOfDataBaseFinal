import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from '../../../core/i18n/locales';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { Seo } from '../../../core/seo/seo';
import type { SeoPage } from '../../../core/seo/seo-page';
import { PATCHES_SCOPE } from '../patches-scope';

/** The page's path below the locale, its canonical one. */
const PATCHES_PATH = 'patches';

type Translate = (key: string) => string;

function patchHistoryHead(translate: Translate): SeoPage {
  return {
    title: translate('patches.title'),
    description: translate('patches.description'),
    path: PATCHES_PATH,
    jsonLd: (urls) => [
      breadcrumbList([
        { name: translate('header.navigation.home'), url: urls.page('') },
        { name: translate('patches.title'), url: urls.canonical },
      ]),
    ],
  };
}

/**
 * Writes the indexable head of `/{locale}/patches` (canonical, alternates, breadcrumb) once
 * its catalogues are loaded, the root one and the page's, and again when the router reuses
 * the page for another locale. Called from the page's constructor: the server render waits.
 */
export function applyPatchHistoryHead(locale: () => Locale): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  effect(() => {
    const current = locale();
    tasks.run(async () => {
      const catalogues = [current, `${PATCHES_SCOPE}/${current}`];
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        catalogues.map((path) => firstValueFrom(transloco.load(path)).catch(() => undefined)),
      );
      await seo.apply(patchHistoryHead((key) => transloco.translate(key, {}, current)));
    });
  });
}
