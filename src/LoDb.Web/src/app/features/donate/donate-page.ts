import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional `/{locale}/donate` page (L3.1). The payments chantier (L6.5) replaces it and
 * keeps donate.routes.ts.
 */
@Component({
  selector: 'lodb-donate-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'donate.title' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonatePage {}
