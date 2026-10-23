import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  type ElementRef,
  type OnInit,
  afterRenderEffect,
  computed,
  contentChild,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { Button } from '../../../../ui/controls/button';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { EmptyState } from '../../../../ui/surfaces/empty-state';
import { Skeleton } from '../../../../ui/surfaces/skeleton';
import { FilterConsole } from '../console/filter-console';
import { FilterSheet } from '../console/filter-sheet';
import { FilterSearch } from '../console/filter-search';
import type { FacetDefinition } from '../facets/model/facet-definition';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { ActiveFilters } from '../results/active-filters';
import { FilterToolbar } from '../results/filter-toolbar';
import { PageSizePicker } from '../results/page-size-picker';
import { isSearchShortcut } from '../search/is-search-shortcut';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import { DEFAULT_PAGE_SIZE } from '../source/default-page-size';
import type { CatalogueCardAdapter } from '../state/catalogue-card-adapter';
import { CatalogueFilter } from '../state/catalogue-filter';
import type { CatalogueListLike } from '../state/catalogue-list-like';
import { PAGE_SIZES } from '../state/page-sizes';
import { FilterUrlSync } from '../url/sync/filter-url-sync';
import { CatalogueCardTemplate } from './catalogue-card-template';

/** Beyond a screenful, more placeholder tiles only lengthen the page. */
const MAX_SKELETON_TILES = 24;
/** The smallest page size offered, first of the list. */
const [SMALLEST_PAGE_SIZE] = PAGE_SIZES;

/**
 * The filterable list of a catalogue page: the filter rail (a sticky search bar and a
 * bottom sheet on narrow screens), the results head, and the grid of the cards its page
 * draws. The first page comes from the server as is, readable by crawlers; the whole list
 * then filters in place, and the URL keeps the state (`q`, `page`, `size`, `<key>`,
 * `<key>_all`), so a filtered list is a link. `/` jumps to the search.
 *
 * The rail's console is hydrated once the reader points at it, focuses, clicks or types in
 * it, or presses `/`, else once its server-rendered markup is out of date (the whole list
 * read, the state moved): until then that markup costs no script, during the page's load
 * above all.
 */
@Component({
  selector: 'lodb-catalogue-list',
  imports: [
    ActiveFilters,
    Button,
    EmptyState,
    FilterConsole,
    FilterSearch,
    FilterToolbar,
    NgTemplateOutlet,
    PageSizePicker,
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
  /** Accessible name of the search, such as "Search for an item…". */
  readonly searchLabel = input.required<string>();
  /** Accessible name of the results, such as "Items". */
  readonly label = input.required<string>();
  /** `stack` lays the cards out one per row, such as an accordion of spells. */
  readonly layout = input<'grid' | 'stack'>('grid');
  /**
   * Columns of the grid per viewport width, as the legacy lists set them: `entity` rows
   * (1 to 4) for items, spells and runes, `portrait` art cards (2 to 5) for champions.
   */
  readonly columns = input<'entity' | 'portrait'>('entity');
  /** Which side the rail sits on, on wide screens: the inline end, as on every legacy list. */
  readonly rail = input<'start' | 'end'>('end');

  protected readonly filter = inject(CatalogueFilter);
  protected readonly cardTemplate = contentChild.required(CatalogueCardTemplate);
  private readonly document = inject(DOCUMENT);
  private readonly dialogs = inject(DialogService);
  // By reference, not by class: the console's code stays in the chunk its defer block loads.
  private readonly console = viewChild<FilterConsole>('console');
  private readonly consoleRail = viewChild.required<ElementRef<HTMLElement>>('consoleRail');
  private readonly barSearch = viewChild.required(FilterSearch);
  /** `/` was pressed before the console was hydrated: it hydrates, then takes the focus. */
  protected readonly isConsoleWanted = signal(false);
  /**
   * What the server drew the rail from has changed: the whole list came (the counts and
   * options were the first page's), or the state moved without the rail (a filter cleared
   * from the results, another language). Left dehydrated, the rail would show its old copy.
   */
  private readonly isRailOutdated = linkedSignal<unknown, boolean>({
    source: () => [this.source().dataset(), this.filter.state()],
    computation: (_, previous) => previous !== undefined,
  });
  protected readonly isConsoleDue = computed(() => this.isConsoleWanted() || this.isRailOutdated());

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
  /**
   * The page sizes come again under the grid, where the reader who went through it stands,
   * unless the smallest page already holds every match: then no size changes anything.
   */
  protected readonly isSizePickerRepeated = computed(
    () => this.filter.view().matching > SMALLEST_PAGE_SIZE,
  );

  constructor() {
    afterRenderEffect(() => {
      const filterConsole = this.console();
      if (filterConsole !== undefined && this.isConsoleWanted()) {
        this.isConsoleWanted.set(false);
        filterConsole.search().focus();
      }
    });
  }

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
    this.filter.anticipate();
    this.dialogs.openSheet(FilterSheet, { labelledBy: FilterSheet.HEADING_ID, data: this.filter });
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (!isSearchShortcut(event, this.document.activeElement)) {
      return;
    }
    const search = [this.console()?.search(), this.barSearch()].find((field) => field?.isShown());
    if (search !== undefined) {
      event.preventDefault();
      search.focus();
    } else if (this.consoleRail().nativeElement.getClientRects().length > 0) {
      event.preventDefault();
      this.isConsoleWanted.set(true);
    }
  }
}
