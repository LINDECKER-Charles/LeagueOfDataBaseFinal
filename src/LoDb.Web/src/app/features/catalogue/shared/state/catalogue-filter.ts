import { Injectable, computed, inject, signal, untracked } from '@angular/core';
import type { FacetDefinition } from '../facets/model/facet-definition';
import type { FacetState } from '../facets/model/facet-state';
import type { RangeSelection } from '../facets/model/range-selection';
import { activeFacetCount } from '../facets/rules/active-facet-count';
import { withChoiceMatchAll } from '../facets/transitions/with-choice-match-all';
import { withChoiceToggled } from '../facets/transitions/with-choice-toggled';
import { withRange } from '../facets/transitions/with-range';
import { withSelection } from '../facets/transitions/with-selection';
import { withToggle } from '../facets/transitions/with-toggle';
import { collectUniverse } from '../filtering/counts/collect-universe';
import { countFacetOptions } from '../filtering/counts/count-facet-options';
import { offeredFacets } from '../filtering/counts/offered-facets';
import type { FilterCriteria } from '../filtering/grid/filter-criteria';
import { isFilterEngaged } from '../filtering/grid/is-filter-engaged';
import { DEFAULT_PAGE_SIZE } from '../source/default-page-size';
import type { FilterUrlSpec } from '../url/filter-url-spec';
import type { FilterUrlState } from '../url/filter-url-state';
import { FilterUrlSync } from '../url/sync/filter-url-sync';
import { parseFilterUrl } from '../url/parse-filter-url';
import type { CatalogueFilterConfig } from './catalogue-filter-config';
import { filterableCardsOf } from './filterable-cards-of';
import { gridViewOf } from './grid-view-of';

const BLANK: FilterUrlState = { query: '', facets: {}, page: 1, size: DEFAULT_PAGE_SIZE };

/**
 * The state of one filtered list, provided by lodb-catalogue-list next to its FilterUrlSync
 * and read by every part of it (console, sheet, toolbar, grid). The state is the truth: it
 * starts from the URL, each action changes it and asks the URL to follow; a navigation the
 * list did not ask for hands it back.
 */
@Injectable()
export class CatalogueFilter {
  private readonly urlSync = inject(FilterUrlSync);
  private readonly initialSearch = this.urlSync.search();
  private readonly config = signal<CatalogueFilterConfig<unknown> | null>(null);
  private readonly adopted = signal<FilterUrlState | null>(null);

  readonly schema = computed(() => this.config()?.schema() ?? []);
  private readonly source = computed(() => this.config()?.source() ?? null);
  private readonly spec = computed<FilterUrlSpec>(() => ({
    schema: this.schema(),
    defaultSize: this.source()?.defaultSize ?? DEFAULT_PAGE_SIZE,
  }));
  // Read once, when the list connects: a schema relabelled by a language switch must not
  // put the reader back to the state of the URL they arrived with.
  private readonly initial = computed(() =>
    this.config() === null
      ? null
      : untracked(() => parseFilterUrl(this.initialSearch, this.spec())),
  );

  readonly state = computed(() => this.adopted() ?? this.initial() ?? BLANK);
  readonly status = computed(() => this.source()?.status() ?? 'loading');
  readonly activeCount = computed(() => activeFacetCount(this.state().facets));
  readonly isEngaged = computed(() => isFilterEngaged(this.state().query, this.state().facets));

  private readonly criteria = computed<FilterCriteria>(() => ({
    query: this.state().query,
    facets: this.state().facets,
    schema: this.schema(),
  }));
  private readonly allCards = computed(() => {
    const config = this.config();
    const dataset = this.source()?.dataset() ?? null;
    return config === null || dataset === null
      ? null
      : filterableCardsOf(dataset.entries, config.adapter());
  });
  // Before the whole list, the facets read the page the server rendered.
  private readonly knownCards = computed(() => {
    const config = this.config();
    const first = this.source()?.firstPage() ?? null;
    return (
      this.allCards() ?? (config && first ? filterableCardsOf(first.entries, config.adapter()) : [])
    );
  });

  readonly universe = computed(() => collectUniverse(this.knownCards()));
  readonly offered = computed(() => offeredFacets(this.schema(), this.universe()));
  readonly counts = computed(() => countFacetOptions(this.knownCards(), this.criteria()));
  readonly view = computed(() =>
    gridViewOf({
      cards: this.allCards(),
      first: this.source()?.firstPage() ?? null,
      slice: this.source()?.slice ?? { page: BLANK.page, size: BLANK.size },
      state: this.state(),
      schema: this.schema(),
    }),
  );

  /** Called once by the list: from then on the state and the URL follow each other. */
  connect<C>(config: CatalogueFilterConfig<C>): void {
    this.config.set(config);
    this.urlSync.bind({
      spec: () => this.spec(),
      current: () => this.state(),
      adopt: (state) => this.adopted.set(state),
    });
  }

  keyOf(card: unknown): string {
    return this.config()?.adapter().keyOf(card) ?? '';
  }

  /** The link of the list in another state, such as another page. */
  urlOf(change: Partial<FilterUrlState>): string {
    return this.urlSync.urlOf({ ...this.state(), ...change });
  }

  setQuery(query: string): void {
    this.change({ query, page: 1 });
  }

  toggleChoice(facet: FacetDefinition, value: string): void {
    this.changeFacets(withChoiceToggled(this.state().facets, facet, value));
  }

  setMatchAll(key: string, all: boolean): void {
    this.changeFacets(withChoiceMatchAll(this.state().facets, key, all));
  }

  setRange(key: string, range: RangeSelection | null): void {
    this.changeFacets(withRange(this.state().facets, key, range));
  }

  setToggle(key: string, isOn: boolean): void {
    this.changeFacets(withToggle(this.state().facets, key, isOn));
  }

  clearFacet(key: string): void {
    this.changeFacets(withSelection(this.state().facets, key, undefined));
  }

  clearAll(): void {
    this.change({ query: '', facets: {}, page: 1 });
  }

  setSize(size: number): void {
    this.change({ size, page: 1 });
  }

  goTo(page: number): void {
    const target = Math.min(Math.max(1, page), this.view().pageCount);
    this.change({ page: target });
  }

  // Narrowing the list sends the reader back to its first page.
  private changeFacets(facets: FacetState): void {
    this.change({ facets, page: 1 });
  }

  private change(change: Partial<FilterUrlState>): void {
    this.adopted.set({ ...this.state(), ...change });
    this.urlSync.request();
  }
}
