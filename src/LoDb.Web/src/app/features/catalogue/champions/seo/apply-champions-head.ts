import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Seo } from '../../../../core/seo/seo';
import type { HeadSource } from './head-source';
import type { Translate } from './translate';

/**
 * Writes the head of a champions page each time its source changes (another champion, a
 * list that arrived, another locale), once the root catalogue and the `seo` scope its texts
 * come from are loaded. Call it from the page's constructor: the render waits for the head.
 */
export function applyChampionsHead(source: () => HeadSource | null): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  effect(() => {
    const head = source();
    if (head === null) {
      return;
    }
    const { locale, build } = head;
    tasks.run(async () => {
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        [locale, `seo/${locale}`].map((path) =>
          firstValueFrom(transloco.load(path)).catch(() => undefined),
        ),
      );
      const translate: Translate = (key, params = {}) => transloco.translate(key, params, locale);
      await seo.apply(build(translate));
    });
  });
}
