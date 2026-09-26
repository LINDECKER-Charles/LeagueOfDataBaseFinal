import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional home page (L3.1): it proves the route, the locale and the server render end to
 * end. The home chantier (L3.9) replaces it and keeps home.routes.ts.
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
export class HomePage {}
