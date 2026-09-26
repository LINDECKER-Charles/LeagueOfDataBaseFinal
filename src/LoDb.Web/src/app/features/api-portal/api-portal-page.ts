import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional API key portal, `/{locale}/account/api`, rendered in the browser only (L3.1).
 * The developer portal chantier (L6.4) replaces it and keeps api-portal.routes.ts; its credit
 * checkout shows only where the build allows payments (`PAYMENTS_ENABLED`, false in the
 * store build of the apps, ADR 0007), the keys themselves everywhere.
 */
@Component({
  selector: 'lodb-api-portal-page',
  imports: [TranslocoPipe],
  providers: [provideTranslocoScope('api')],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'api.portal.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiPortalPage {}
