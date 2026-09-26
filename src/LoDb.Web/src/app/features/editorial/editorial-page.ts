import { ChangeDetectionStrategy, Component, PendingTasks, effect, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { Seo } from '../../core/seo/seo';

/** The Transloco scope some of the headings live in (`public/i18n/about/`). */
const ABOUT_SCOPE = 'about';

/**
 * Provisional page of every editorial route, prerendered for the 21 locales (L3.1), with
 * its head: canonical on its own path, 21 alternates and `x-default`. The editorial chantier
 * (L3.10) replaces it, keeps editorial.routes.ts and its call to {@link Seo}.
 */
@Component({
  selector: 'lodb-editorial-page',
  imports: [TranslocoPipe],
  providers: [provideTranslocoScope(ABOUT_SCOPE)],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ heading() | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorialPage {
  /** Translation key of the page title, declared by its route. */
  protected readonly heading = injectRouteData<string>('heading');

  constructor() {
    const seo = inject(Seo);
    const transloco = inject(TranslocoService);
    const tasks = inject(PendingTasks);
    const page = inject(PageDirection);
    // Each editorial route declares a static path, below the locale: its canonical path.
    const path = inject(ActivatedRoute).snapshot.routeConfig?.path ?? '';
    // The router reuses the page from one locale to another.
    effect(() => {
      const locale = page.locale();
      const heading = this.heading();
      tasks.run(async () => {
        // The headings of the scope are merged into the locale's catalogue, prefixed.
        const catalogues = [locale, `${ABOUT_SCOPE}/${locale}`];
        await Promise.all(
          catalogues.map((catalogue) =>
            firstValueFrom(transloco.load(catalogue)).catch(() => undefined),
          ),
        );
        await seo.apply({ title: transloco.translate(heading, {}, locale), path, locale });
      });
    });
  }
}
