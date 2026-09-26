import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional admin page, `/admin`, outside the locales and rendered in the browser only
 * (L3.1). The admin front (L7.4) replaces it and keeps admin.routes.ts.
 */
@Component({
  selector: 'lodb-admin-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'base.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPage {}
