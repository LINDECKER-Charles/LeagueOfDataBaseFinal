import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { injectRouteData } from '../../../core/routing/inject-route-data';

/**
 * Provisional page of the builds of an account, `/{locale}/account/builds/**`, rendered in
 * the browser only (L3.1). The editor chantier (L5.2) replaces it and keeps editor.routes.ts.
 */
@Component({
  selector: 'lodb-editor-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ heading() | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorPage {
  /** Translation key of the page title, declared by its route. */
  protected readonly heading = injectRouteData<string>('heading');
}
