import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogueFilter } from '../state/catalogue-filter';
import { FacetPanel } from './facets/facet-panel';
import { FilterSearch } from './filter-search';

const PERCENT = 100;

/**
 * The filter rail of a list on wide screens, sticky beside the results: the search, a gauge
 * of what is left, the facets, and a way to let everything go.
 */
@Component({
  selector: 'lodb-filter-console',
  imports: [FacetPanel, FilterSearch, TranslocoPipe],
  templateUrl: './filter-console.html',
  styleUrls: ['./marks.css', './filter-console.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterConsole {
  readonly searchLabel = input.required<string>();
  protected readonly filter = inject(CatalogueFilter);
  /** The rail's search field, for the `/` shortcut. */
  readonly search = viewChild.required(FilterSearch);

  protected readonly gauge = computed(() => {
    const { matching, total } = this.filter.view();
    return total === 0 ? 0 : (matching / total) * PERCENT;
  });
}
