import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional trends page, `/{locale}/trends` (L3.1). The trends chantier (L5.3) replaces it
 * and keeps trends.routes.ts.
 */
@Component({
  selector: 'lodb-trends-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'community.trends.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrendsPage {}
