import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemDetails } from '../../../core/api/generated/models/item-details';
import type { PageContext } from '../../../core/context/page-context';
import type { CatalogueEntry } from '../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../core/routing/inject-route-data';

/**
 * Provisional page of the item list and details (L3.1): it shows what the resolvers handed it,
 * the item's name and the version read. The item pages (L3.7) replace it and keep
 * items.routes.ts.
 */
@Component({
  selector: 'lodb-items-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    @if (entry(); as current) {
      <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
        {{ current.details.profile.name }}
      </h1>
      <p class="mt-3 text-sm text-text-muted">{{ current.context.version }}</p>
    } @else {
      <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
        {{ 'item.list.title' | transloco }}
      </h1>
      <p class="mt-3 text-sm text-text-muted">{{ context()?.version }}</p>
    }
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemsPage {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext | undefined>('context');
  /** The item of a detail page, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<ItemDetails> | undefined>('entry');
}
