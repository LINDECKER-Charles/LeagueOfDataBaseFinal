import { LowerCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe, translateSignal } from '@jsverse/transloco';
import type { ChampionCard as Card } from '../../../core/api/generated/models/champion-card';
import type { PageContext } from '../../../core/context/page-context';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { Logo } from '../../../ui/media/logo';
import { catalogueHref } from '../shared/links/catalogue-href';
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
  imports: [CatalogueCardTemplate, CatalogueList, ChampionCard, Logo, LowerCasePipe, TranslocoPipe],
  templateUrl: './champions-page.html',
  styleUrl: './champions-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionsPage {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext>('context');
  protected readonly list = injectCatalogueList('champions', this.context);
  protected readonly adapter = CHAMPION_CARD_ADAPTER;
  protected readonly eagerCards = EAGER_CARDS;
  protected readonly total = computed(() => this.list.list()?.total ?? null);
  private readonly router = inject(Router);
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

  protected href(card: Card): string {
    return catalogueHref(this.context(), card.canonicalPath, this.router.url);
  }
}
