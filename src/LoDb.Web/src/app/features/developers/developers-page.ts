import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional `/{locale}/developers` page (L3.1). The developer portal chantier (L6.4)
 * replaces it and keeps developers.routes.ts.
 */
@Component({
  selector: 'lodb-developers-page',
  imports: [TranslocoPipe],
  providers: [provideTranslocoScope('api')],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'api.developers.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DevelopersPage {}
