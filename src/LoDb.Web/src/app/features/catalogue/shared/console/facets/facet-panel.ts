import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { countEngaged } from '../../facets/rules/count-engaged';
import { groupFacets } from '../../facets/rules/group-facets';
import { isGroupOpenByDefault } from '../../facets/rules/is-group-open-by-default';
import { pinEngagedGroups } from '../../facets/rules/pin-engaged-groups';
import { CatalogueFilter } from '../../state/catalogue-filter';
import { FacetChoice } from './facet-choice';
import { FacetGroup } from './facet-group';
import { FacetRange } from './facet-range';
import { FacetToggle } from './facet-toggle';

/**
 * The facets the list can be narrowed by, under their group headings. A group holding a
 * main axis or an engaged facet starts unfolded and, once engaged, stays so until the reader
 * folds it; the reader's folding is kept per surface. Folded groups stay in the DOM.
 */
@Component({
  selector: 'lodb-facet-panel',
  imports: [FacetChoice, FacetGroup, FacetRange, FacetToggle],
  template: `@for (group of groups(); track group.name; let index = $index) {
    <lodb-facet-group
      [name]="group.name"
      [engaged]="group.engaged"
      [open]="group.isOpen"
      [groupId]="'lodb-facets-' + surface() + '-' + index"
      (toggled)="fold(group.name, !group.isOpen)"
    >
      @for (facet of group.facets; track facet.key) {
        @switch (facet.kind) {
          @case ('choice') {
            <lodb-facet-choice [facet]="facet" />
          }
          @case ('range') {
            <lodb-facet-range [facet]="facet" />
          }
          @case ('toggle') {
            <lodb-facet-toggle [facet]="facet" />
          }
        }
      }
    </lodb-facet-group>
  }`,
  host: { class: 'flex flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetPanel {
  /** Where the panel is drawn: the rail and the sheet remember their folding apart. */
  readonly surface = input<'rail' | 'sheet'>('rail');

  private readonly filter = inject(CatalogueFilter);
  private readonly folds = computed(() => this.filter.folds[this.surface()]);
  private readonly offeredGroups = computed(() => groupFacets(this.filter.offered()));

  protected readonly groups = computed(() => {
    const facets = this.filter.state().facets;
    const folds = this.folds()();
    return this.offeredGroups().map((group) => ({
      ...group,
      engaged: countEngaged(group.facets, facets),
      isOpen: folds[group.name] ?? isGroupOpenByDefault(group, facets),
    }));
  });

  constructor() {
    // An engagement pins its group open, so the default never folds it back.
    effect(() => {
      const folds = this.folds();
      const pinned = pinEngagedGroups(this.offeredGroups(), this.filter.state().facets, folds());
      if (pinned !== null) {
        folds.set(pinned);
      }
    });
  }

  protected fold(name: string, isOpen: boolean): void {
    this.folds().update((folds) => ({ ...folds, [name]: isOpen }));
  }
}
