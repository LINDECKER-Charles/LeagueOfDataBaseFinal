import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Field } from '../../../../../ui/controls/field';
import type { FacetDefinition } from '../../facets/model/facet-definition';
import { CatalogueFilter } from '../../state/catalogue-filter';

/** One flag facet as a switch ("purchasable only"), with the count it keeps. */
@Component({
  selector: 'lodb-facet-toggle',
  imports: [Field],
  template: `<label class="facet toggle" [class.toggle--on]="isOn()">
    <input
      type="checkbox"
      lodbField="switch"
      [checked]="isOn()"
      (change)="filter.setToggle(facet().key, box.checked)"
      #box
    />
    <span class="toggle__label">{{ facet().label }}</span>
    <span class="facet__count">{{ count() }}</span>
  </label>`,
  styleUrls: ['./facet.css'],
  styles: `
    .toggle {
      flex-direction: row;
      align-items: center;
      gap: 0.6rem;
      padding-block: 0.1rem;
      cursor: pointer;
    }
    .toggle__label {
      font-family: var(--font-mono);
      font-size: 0.72rem;
      color: var(--color-text-muted);
    }
    .toggle--on .toggle__label {
      color: var(--color-text);
    }
    @media (pointer: coarse) {
      .toggle {
        padding-block: 0.35rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetToggle {
  readonly facet = input.required<FacetDefinition>();
  protected readonly filter = inject(CatalogueFilter);

  protected readonly isOn = computed(() => this.filter.state().facets[this.facet().key] === true);
  protected readonly count = computed(() => this.filter.counts().flagged[this.facet().key] ?? 0);
}
