import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import type { ItemDetails } from '../../../core/api/generated/models/item-details';
import type { CatalogueEntry } from '../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { ItemDetail } from './detail/item-detail';
import { ItemList } from './list/item-list';

/**
 * The item pages (items.routes.ts): the details of the item a route resolved, else the list.
 * Both read the feature's own texts, `public/i18n/items/`.
 */
@Component({
  selector: 'lodb-items-page',
  imports: [ItemDetail, ItemList],
  providers: [provideTranslocoScope('items')],
  template: `@if (entry()) {
      <lodb-item-detail />
    } @else {
      <lodb-item-list />
    }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemsPage {
  /** The item of a detail page, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<ItemDetails> | undefined>('entry');
}
