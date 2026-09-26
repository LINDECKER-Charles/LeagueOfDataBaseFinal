import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { FacetDefinition } from '../../facets/model/facet-definition';
import { isChoiceSelection } from '../../facets/rules/is-choice-selection';
import { choiceRowsOf } from '../../filtering/counts/choice-rows-of';
import { CatalogueFilter } from '../../state/catalogue-filter';

const NOTHING_PRESENT: ReadonlySet<string> = new Set();
const NO_COUNTS: ReadonlyMap<string, number> = new Map();
// Below two picked values, "all" and "any" keep the same cards.
const MATCH_MODE_MIN_VALUES = 2;

/**
 * One choice facet as a row of counted chips: only the values the list carries, in the
 * schema's order. A value no card in the current context carries stays visible, disabled.
 */
@Component({
  selector: 'lodb-facet-choice',
  imports: [TranslocoPipe],
  templateUrl: './facet-choice.html',
  styleUrls: ['./facet.css', './facet-choice.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetChoice {
  readonly facet = input.required<FacetDefinition>();
  protected readonly filter = inject(CatalogueFilter);

  private readonly selection = computed(() => {
    const selection = this.filter.state().facets[this.facet().key];
    return selection !== undefined && isChoiceSelection(selection) ? selection : null;
  });
  protected readonly matchesAll = computed(() => this.selection()?.all === true);
  // Kept while match-all is on: its disabled chips must not hide the way back.
  protected readonly showsMatchMode = computed(
    () =>
      this.facet().matchAll &&
      ((this.selection()?.values.length ?? 0) >= MATCH_MODE_MIN_VALUES || this.matchesAll()),
  );
  protected readonly rows = computed(() => {
    const facet = this.facet();
    return choiceRowsOf({
      facet,
      present: this.filter.universe().present[facet.key] ?? NOTHING_PRESENT,
      selected: this.selection()?.values ?? [],
      counts: this.filter.counts().options[facet.key] ?? NO_COUNTS,
    });
  });
}
