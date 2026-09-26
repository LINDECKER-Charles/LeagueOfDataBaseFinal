import { LowerCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RuneCard as RuneCardModel } from '../../../../core/api/generated/models/rune-card';
import type { RuneTreeCard } from '../../../../core/api/generated/models/rune-tree-card';
import type { PageContext } from '../../../../core/context/page-context';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { injectListHead } from '../../items/codex/head/inject-list-head';
import { ListHeading } from '../../items/codex/heading/list-heading';
import { injectCatalogueLink } from '../../items/codex/links/inject-catalogue-link';
import { injectTranslate } from '../../items/codex/texts/inject-translate';
import { CatalogueImage } from '../../shared/cards/catalogue-image';
import { CatalogueCardTemplate } from '../../shared/list/catalogue-card-template';
import { CatalogueList } from '../../shared/list/catalogue-list';
import { injectCatalogueList } from '../../shared/source/inject-catalogue-list';
import { slotLabelOf } from '../paths/slot-label-of';
import { RUNE_CARD_ADAPTER } from './rune-card-adapter';
import { RuneCard } from './rune-card';
import { runeFacetsOf } from './rune-facets-of';

/** Cards of the first row, whose icons are loaded at once. */
const EAGER_CARDS = 4;
/** Box of a path's mark in the banner, in CSS pixels. */
const PATH_ICON_SIZE = 20;

/**
 * The rune list, `/{locale}/[{version}/]runes`: the banner of the five paths, the pages
 * every card leads into, then every rune, filterable by path and row.
 */
@Component({
  selector: 'lodb-rune-list',
  imports: [
    Backdrop,
    CatalogueCardTemplate,
    CatalogueImage,
    CatalogueList,
    ListHeading,
    LowerCasePipe,
    RouterLink,
    RuneCard,
    TranslocoPipe,
  ],
  templateUrl: './rune-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneList {
  /** Context of the list, resolved by `resolveCatalogueContext`. */
  protected readonly context = injectRouteData<PageContext | undefined>('context');
  protected readonly list = injectCatalogueList('runes', this.context);
  protected readonly adapter = RUNE_CARD_ADAPTER;
  protected readonly eagerCards = EAGER_CARDS;
  protected readonly pathIconSize = PATH_ICON_SIZE;
  protected readonly linkOf = injectCatalogueLink();

  private readonly translate = injectTranslate();

  /** The paths, known from the first page the server rendered on. */
  protected readonly trees = computed<readonly RuneTreeCard[]>(
    () => (this.list.list() ?? this.list.firstPage())?.trees ?? [],
  );
  protected readonly schema = computed(() => runeFacetsOf(this.trees(), this.translate()));
  private readonly treeNames = computed(
    () => new Map(this.trees().map((tree) => [tree.key, tree.name])),
  );

  constructor() {
    injectListHead(() => {
      const context = this.context();
      const status = this.list.status();
      if (context === undefined || status === 'loading') {
        return null;
      }
      const trees = this.list.firstPage()?.trees ?? null;
      const count = trees?.length ?? null;
      return { context, texts: 'rune', path: 'runes', count, entries: trees ?? [] };
    });
  }

  protected captionOf(card: RuneCardModel): string {
    const path = this.treeNames().get(card.tree) ?? card.tree;
    return `${path} · ${slotLabelOf(card.slot, this.translate())}`;
  }
}
