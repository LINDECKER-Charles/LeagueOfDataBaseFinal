import { ChangeDetectionStrategy, Component, PendingTasks, effect, inject } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { Seo } from '../../core/seo/seo';

/**
 * Provisional home page (L3.1): it proves the route, the locale and the server render end to
 * end, and writes the home's head (canonical `/{locale}/`, 21 alternates and `x-default`).
 * The home chantier (L3.9) replaces it, keeps home.routes.ts and its call to {@link Seo}.
 */
@Component({
  selector: 'lodb-home-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'homepage.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  constructor() {
    const seo = inject(Seo);
    const transloco = inject(TranslocoService);
    const tasks = inject(PendingTasks);
    const page = inject(PageDirection);
    // The router reuses the page from one locale's home to another's.
    effect(() => {
      const locale = page.locale();
      tasks.run(async () => {
        await firstValueFrom(transloco.load(locale)).catch(() => undefined);
        // The heading is the whole document title: no site name appended.
        const title = transloco.translate('homepage.title', {}, locale);
        await seo.apply({ title, path: '', titleFormat: 'raw', locale });
      });
    });
  }
}
