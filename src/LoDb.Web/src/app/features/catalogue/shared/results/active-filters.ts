import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogueFilter } from '../state/catalogue-filter';
import { describeSelection } from './describe-selection';

/** What filters the list, as chips that each let one criterion go, above the results. */
@Component({
  selector: 'lodb-active-filters',
  imports: [TranslocoPipe],
  template: `<div class="active" role="group" [attr.aria-label]="'filter.active' | transloco">
    <span class="marker marker--on" aria-hidden="true"></span>
    @if (query() !== '') {
      <button type="button" class="active__chip" (click)="filter.setQuery('')">
        <span>“{{ query() }}”</span>
        <span class="active__x" aria-hidden="true">×</span>
      </button>
    }
    @for (chip of chips(); track chip.key) {
      <button type="button" class="active__chip" (click)="filter.clearFacet(chip.key)">
        <span>{{ chip.text }}</span>
        <span class="active__x" aria-hidden="true">×</span>
      </button>
    }
    <button type="button" class="clear" (click)="filter.clearAll()">
      {{ 'filter.clear_all' | transloco }}
    </button>
  </div>`,
  styleUrls: ['../console/marks.css', './active-filters.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActiveFilters {
  protected readonly filter = inject(CatalogueFilter);

  protected readonly query = computed(() => this.filter.state().query.trim());
  protected readonly chips = computed(() => {
    const facets = this.filter.state().facets;
    return this.filter.schema().flatMap((facet) => {
      const selection = facets[facet.key];
      return selection === undefined
        ? []
        : [{ key: facet.key, text: describeSelection(facet, selection) }];
    });
  });
}
