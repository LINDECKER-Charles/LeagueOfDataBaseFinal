import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { injectRouteData } from '../../core/routing/inject-route-data';

/**
 * Provisional page of every editorial route, prerendered for the 21 locales (L3.1). The
 * editorial chantier (L3.10) replaces it and keeps editorial.routes.ts.
 */
@Component({
  selector: 'lodb-editorial-page',
  imports: [TranslocoPipe],
  providers: [provideTranslocoScope('about')],
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
}
