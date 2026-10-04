import { PendingTasks, type Signal, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { Seo } from '../../../core/seo/seo';
import { profileSeoPage } from './profile-seo-page';

/**
 * Writes the head of a public card once the root catalogue of its locale is loaded, and
 * again when the router reuses the page for another card or locale. Called in the page's
 * constructor: the server render waits for the head.
 */
export function applyProfileHead(profile: Signal<PublicProfile>): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const card = profile();
    const locale = page.locale();
    tasks.run(async () => {
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await firstValueFrom(transloco.load(locale)).catch(() => undefined);
      const translate = (key: string, params: Record<string, string> = {}) =>
        transloco.translate(key, params, locale);
      await seo.apply(profileSeoPage(card, translate, locale));
    });
  });
}
