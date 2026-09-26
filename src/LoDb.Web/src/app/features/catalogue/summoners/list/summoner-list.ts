import { LowerCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import type { UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { SummonerCard } from '../../../../core/api/generated/models/summoner-card';
import type { PageContext } from '../../../../core/context/page-context';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Accordion } from '../../../../ui/accordion/accordion';
import { AccordionItem } from '../../../../ui/accordion/accordion-item';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { injectListHead } from '../../shared/codex/head/inject-list-head';
import { ListHeading } from '../../shared/codex/heading/list-heading';
import { RichText } from '../../shared/codex/rich-text/rich-text';
import { injectCatalogueLink } from '../../shared/codex/links/inject-catalogue-link';
import { injectTranslate } from '../../shared/codex/texts/inject-translate';
import { EntityCard } from '../../shared/cards/entity-card';
import { CatalogueCardTemplate } from '../../shared/list/catalogue-card-template';
import { CatalogueList } from '../../shared/list/catalogue-list';
import { injectCatalogueList } from '../../shared/source/inject-catalogue-list';
import { modeLabelsOf } from '../modes/mode-labels-of';
import { cooldownTextOf } from './cooldown-text-of';
import { SUMMONER_CARD_ADAPTER } from './summoner-card-adapter';
import { summonerFacetsOf } from './summoner-facets-of';

/** Cards of the first row, whose icons are loaded at once. */
const EAGER_CARDS = 4;
const NO_FACETS = { levels: [], modes: [] };

/**
 * The summoner spell list, `/{locale}/[{version}/]summoners`: every spell, LoL Classic twins
 * included and marked, each card with its cooldown, unlock level, modes and description,
 * filterable by mode, edition, level and cooldown.
 */
@Component({
  selector: 'lodb-summoner-list',
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
  templateUrl: './summoner-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SummonerList {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext | undefined>('context');
  protected readonly list = injectCatalogueList('summoners', this.context);
  protected readonly adapter = SUMMONER_CARD_ADAPTER;
  protected readonly eagerCards = EAGER_CARDS;
  protected readonly cooldownText = cooldownTextOf;

  private readonly translate = injectTranslate();
  private readonly linkOf = injectCatalogueLink();

  protected readonly schema = computed(() => {
    const list = this.list.list() ?? this.list.firstPage();
    return summonerFacetsOf(list?.facets ?? NO_FACETS, list?.entries ?? [], this.translate());
  });

  constructor() {
    injectListHead(() => {
      const context = this.context();
      const status = this.list.status();
      if (context === undefined || status === 'loading') {
        return null;
      }
      const page = this.list.firstPage();
      const count = page?.total ?? null;
      return { context, texts: 'summoner', path: 'summoners', count, entries: page?.entries ?? [] };
    });
  }

  protected modesOf(card: SummonerCard): string[] {
    return modeLabelsOf(card.modes, this.translate()('edition.classic'));
  }

  protected hrefOf(context: PageContext, card: SummonerCard): UrlTree {
    return this.linkOf(context, card.canonicalPath);
  }
}
