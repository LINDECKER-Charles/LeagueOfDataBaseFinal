import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Accordion } from '../../../../../ui/accordion/accordion';
import { AccordionItem } from '../../../../../ui/accordion/accordion-item';
import { countEngaged } from '../../facets/rules/count-engaged';
import { groupFacets } from '../../facets/rules/group-facets';
import { isGroupOpenByDefault } from '../../facets/rules/is-group-open-by-default';
import { CatalogueFilter } from '../../state/catalogue-filter';
import { FacetChoice } from './facet-choice';
import { FacetRange } from './facet-range';
import { FacetToggle } from './facet-toggle';

/**
 * The facets the list can be narrowed by, under their group headings. A group holding a
 * main axis or an engaged facet starts unfolded; folded ones stay in the DOM.
 */
@Component({
  selector: 'lodb-facet-panel',
  imports: [Accordion, AccordionItem, FacetChoice, FacetRange, FacetToggle],
  template: `<lodb-accordion multi>
    @for (group of groups(); track group.name) {
      <lodb-accordion-item
        [heading]="group.name"
        [count]="group.engaged > 0 ? group.engaged : undefined"
        [expanded]="group.isOpen"
      >
        <div class="flex flex-col gap-4">
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
        </div>
      </lodb-accordion-item>
    }
  </lodb-accordion>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetPanel {
  private readonly filter = inject(CatalogueFilter);

  protected readonly groups = computed(() => {
    const facets = this.filter.state().facets;
    return groupFacets(this.filter.offered()).map((group) => ({
      ...group,
      engaged: countEngaged(group.facets, facets),
      isOpen: isGroupOpenByDefault(group, facets),
    }));
  });
}
