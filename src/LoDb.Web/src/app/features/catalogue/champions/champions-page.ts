import { LowerCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import type { UrlTree } from '@angular/router';
import { TranslocoPipe, translateSignal } from '@jsverse/transloco';
import type { ChampionCard as Card } from '../../../core/api/generated/models/champion-card';
import type { PageContext } from '../../../core/context/page-context';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { Backdrop } from '../../../ui/surfaces/backdrop';
import { ListHeading } from '../shared/codex/heading/list-heading';
import { injectCatalogueLink } from '../shared/codex/links/inject-catalogue-link';
import { CatalogueCardTemplate } from '../shared/list/catalogue-card-template';
import { CatalogueList } from '../shared/list/catalogue-list';
import { injectCatalogueList } from '../shared/source/inject-catalogue-list';
import { ChampionCard } from './list/champion-card';
import { CHAMPION_CARD_ADAPTER } from './list/champion-card-adapter';
import { championFacets } from './list/champion-facets';
import { FACET_LABEL_KEYS } from './list/facet-label-keys';
import { applyChampionsHead } from './seo/apply-champions-head';
import { championsListSeo } from './seo/champions-list-seo';

/** Cards of the first row, whose icons load at once. */
const EAGER_CARDS = 4;

/**
 * The champions list: the page the URL names rendered on the server, readable as is, then
 * the whole list filtered in place by role, resource, range, ratings and base stats.
 */
@Component({
  selector: 'lodb-champions-page',
  imports: [
    Backdrop,
    CatalogueCardTemplate,
    CatalogueList,
    ChampionCard,
    ListHeading,
    LowerCasePipe,
    TranslocoPipe,
  ],
  templateUrl: './champions-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionsPage {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext>('context');
  protected readonly list = injectCatalogueList('champions', this.context);
  protected readonly adapter = CHAMPION_CARD_ADAPTER;
  protected readonly eagerCards = EAGER_CARDS;
  protected readonly total = computed(() => this.list.list()?.total ?? null);
  private readonly linkOf = injectCatalogueLink();
  private readonly labels = translateSignal(FACET_LABEL_KEYS);
  protected readonly schema = computed(() => {
    const labels = this.labels();
    const translate = (key: string) => labels[FACET_LABEL_KEYS.indexOf(key)] ?? key;
    return championFacets(translate, this.list.list()?.entries ?? []);
  });

  constructor() {
    applyChampionsHead(() => {
      const context = this.context();
      const list = this.list.firstPage();
      return {
        locale: context.locale,
        build: (translate) => championsListSeo(context, list, translate),
      };
    });
  }

  protected href(card: Card): UrlTree {
    return this.linkOf(this.context(), card.canonicalPath);
  }
}
