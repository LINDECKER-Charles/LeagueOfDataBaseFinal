import { LowerCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import type { UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { PageContext } from '../../../../core/context/page-context';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Accordion } from '../../../../ui/accordion/accordion';
import { AccordionItem } from '../../../../ui/accordion/accordion-item';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { EntityCard } from '../../shared/cards/entity-card';
import { CatalogueCardTemplate } from '../../shared/list/catalogue-card-template';
import { CatalogueList } from '../../shared/list/catalogue-list';
import { injectCatalogueList } from '../../shared/source/inject-catalogue-list';
import { injectListHead } from '../../shared/codex/head/inject-list-head';
import { ListHeading } from '../../shared/codex/heading/list-heading';
import { RichText } from '../../shared/codex/rich-text/rich-text';
import { injectCatalogueLink } from '../../shared/codex/links/inject-catalogue-link';
import { injectTranslate } from '../../shared/codex/texts/inject-translate';
import { itemFacetsOf } from './facets/item-facets-of';
import { tagLabelOf } from './facets/tag-label-of';
import { ITEM_CARD_ADAPTER } from './item-card-adapter';

/** Cards of the first row, whose icons are loaded at once. */
const EAGER_CARDS = 4;

/**
 * The item list, `/{locale}/[{version}/]items`: every item, LoL Classic twins included and
 * marked, each card with its price, categories and summary, filterable by category,
 * edition, map, tier, price and stats.
 */
@Component({
  selector: 'lodb-item-list',
  imports: [
    Accordion,
    AccordionItem,
    Backdrop,
    CatalogueCardTemplate,
    CatalogueList,
    EntityCard,
    ListHeading,
    LowerCasePipe,
    RichText,
    TranslocoPipe,
  ],
  templateUrl: './item-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemList {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext | undefined>('context');
  protected readonly list = injectCatalogueList('items', this.context);
  protected readonly adapter = ITEM_CARD_ADAPTER;
  protected readonly eagerCards = EAGER_CARDS;
  protected readonly tagLabel = tagLabelOf;

  private readonly translate = injectTranslate();
  private readonly linkOf = injectCatalogueLink();

  protected readonly schema = computed(() =>
    itemFacetsOf(this.list.list()?.facets.tags ?? [], this.translate()),
  );

  constructor() {
    injectListHead(() => {
      const context = this.context();
      const status = this.list.status();
      if (context === undefined || status === 'loading') {
        return null;
      }
      const page = this.list.firstPage();
      const count = page?.total ?? null;
      return { context, texts: 'item', path: 'items', count, entries: page?.entries ?? [] };
    });
  }

  protected hrefOf(context: PageContext, card: ItemCard): UrlTree {
    return this.linkOf(context, card.canonicalPath);
  }
}
