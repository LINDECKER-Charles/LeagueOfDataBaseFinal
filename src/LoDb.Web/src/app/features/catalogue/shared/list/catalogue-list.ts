import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  type OnInit,
  computed,
  contentChild,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { Button } from '../../../../ui/controls/button';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { Skeleton } from '../../../../ui/surfaces/skeleton';
import { FilterConsole } from '../console/filter-console';
import { FilterSheet } from '../console/filter-sheet';
import { FilterSearch } from '../console/filter-search';
import type { FacetDefinition } from '../facets/model/facet-definition';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { ActiveFilters } from '../results/active-filters';
import { FilterToolbar } from '../results/filter-toolbar';
import { isSearchShortcut } from '../search/is-search-shortcut';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import { DEFAULT_PAGE_SIZE } from '../source/default-page-size';
import type { CatalogueCardAdapter } from '../state/catalogue-card-adapter';
import { CatalogueFilter } from '../state/catalogue-filter';
import type { CatalogueListLike } from '../state/catalogue-list-like';
import { FilterUrlSync } from '../url/sync/filter-url-sync';
import { CatalogueCardTemplate } from './catalogue-card-template';

/** Beyond a screenful, more placeholder tiles only lengthen the page. */
const MAX_SKELETON_TILES = 24;

/**
 * The filterable list of a catalogue page: the filter rail (a sticky search bar and a
 * bottom sheet on narrow screens), the results head, and the grid of the cards its page
 * draws. The first page comes from the server as is, readable by crawlers; the whole list
 * then filters in place, and the URL keeps the state (`q`, `page`, `size`, `<key>`,
 * `<key>_all`), so a filtered list is a link. `/` jumps to the search.
 */
@Component({
  selector: 'lodb-catalogue-list',
  imports: [
    ActiveFilters,
    Button,
    FilterConsole,
    FilterSearch,
    FilterToolbar,
    NgTemplateOutlet,
    Skeleton,
    TranslocoPipe,
  ],
  templateUrl: './catalogue-list.html',
  styleUrl: './catalogue-list.css',
  providers: [FilterUrlSync, CatalogueFilter],
  // View providers, not providers: the page's own pipes on this element's inputs, and in the
  // card template it projects, would resolve this scope instead of the page's, and translate
  // its keys (`summoners.search_placeholder`) only if another pipe happened to load them first.
  viewProviders: [provideTranslocoScope('catalogue')],
  host: { class: 'block', '(document:keydown)': 'onKeydown($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueList<C> implements OnInit {
  /** The list, from injectCatalogueList. */
  readonly source = input.required<CatalogueListSource<CatalogueListLike<C>>>();
  readonly adapter = input.required<CatalogueCardAdapter<C>>();
  /** The facets of the list, translated; those no card carries are not offered. */
  readonly schema = input<readonly FacetDefinition[]>([]);
  /** Placeholder and accessible name of the search, such as "Search for an item…". */
  readonly searchLabel = input.required<string>();
  /** Accessible name of the results, such as "Items". */
  readonly label = input.required<string>();
  /** `stack` lays the cards out one per row, such as an accordion of spells. */
  readonly layout = input<'grid' | 'stack'>('grid');
  /** Which side the rail sits on, on wide screens. */
  readonly rail = input<'start' | 'end'>('start');

  protected readonly filter = inject(CatalogueFilter);
  protected readonly cardTemplate = contentChild.required(CatalogueCardTemplate);
  private readonly document = inject(DOCUMENT);
  private readonly dialogs = inject(DialogService);
  private readonly console = viewChild.required(FilterConsole);
  private readonly barSearch = viewChild.required(FilterSearch);

  /** What the list says instead of, or above, its cards when the API could not answer. */
  protected readonly notice = computed(() => {
    const status = this.filter.status();
    if (status === 'pending') {
      return status;
    }
    return status === 'failed' && this.source().list() === null ? status : null;
  });
  protected readonly skeletonSlots = computed(() => {
    const size = this.filter.state().size;
    const tiles = size === PAGE_SIZE_ALL ? DEFAULT_PAGE_SIZE : size;
    return Array.from({ length: Math.min(tiles, MAX_SKELETON_TILES) });
  });

  // Not in the constructor: the first navigation ends before the inputs are set, and the
  // filter reads them as soon as it follows the router.
  ngOnInit(): void {
    this.filter.connect<C>({
      source: () => this.source(),
      adapter: () => this.adapter(),
      schema: () => this.schema(),
    });
  }

  protected openFilters(): void {
    this.dialogs.openSheet(FilterSheet, { labelledBy: FilterSheet.HEADING_ID, data: this.filter });
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (!isSearchShortcut(event, this.document.activeElement)) {
      return;
    }
    const search = [this.console().search(), this.barSearch()].find((field) => field.isShown());
    if (search !== undefined) {
      event.preventDefault();
      search.focus();
    }
  }
}
