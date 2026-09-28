import { PendingTasks, effect, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { Seo } from '../../../core/seo/seo';
import { API_SCOPE } from './api-portal-scope';

const TITLE = 'api.portal.title';

/**
 * Writes the head of the portal, "API keys · League Of Data Base", never indexed nor
 * canonical, once the root and `api` catalogues are loaded, and again on another locale.
 * Called in the page's constructor.
 */
export function applyPortalHead(): void {
  const seo = inject(Seo);
  const transloco = inject(TranslocoService);
  const tasks = inject(PendingTasks);
  const page = inject(PageDirection);
  effect(() => {
    const locale = page.locale();
    tasks.run(async () => {
      // A catalogue that fails leaves its keys to the `en` fallback, never the page headless.
      await Promise.all(
        [locale, `${API_SCOPE}/${locale}`].map((path) =>
          firstValueFrom(transloco.load(path)).catch(() => undefined),
        ),
      );
      await seo.apply({ kind: 'private', locale, title: transloco.translate(TITLE, {}, locale) });
    });
  });
}
