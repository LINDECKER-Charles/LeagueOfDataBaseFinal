import { PendingTasks, type Signal, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Seo } from '../../../../core/seo/seo';
import type { SharedBuildPage } from '../loading/shared-build-page';
import { shareSeoPage } from './share-seo-page';

/**
 * Writes the head of a shared build once the catalogue of its locale is loaded, and again
 * when the router reuses the page for another build. Called in the page's constructor: the
 * server render waits for the head.
 */
export function applyShareHead(shared: Signal<SharedBuildPage>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  effect(() => {
    const { build, locale } = shared();
    tasks.run(async () => {
      await firstValueFrom(transloco.load(locale)).catch(() => undefined);
      const translate = (key: string, params: Record<string, string> = {}) =>
        transloco.translate(key, params, locale);
      await seo.apply(shareSeoPage(build, translate, locale));
    });
  });
}
