import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import type { ChampionCard as Card } from '../../../../core/api/generated/models/champion-card';
import { EntityCard } from '../../shared/cards/entity-card';
import { capitalize } from '../text/capitalize';

/** A champion in the list: its square icon, name and title, then its roles. */
@Component({
  selector: 'lodb-champion-card',
  imports: [EntityCard],
  template: `<lodb-entity-card
    [href]="href()"
    [image]="card().image"
    [name]="card().name"
    [caption]="title()"
    [eager]="eager()"
  >
    @if (card().tags.length > 0) {
      <div class="mt-auto flex flex-wrap gap-1.5 px-4 pb-4">
        @for (tag of card().tags; track tag) {
          <span class="hx-chip">{{ tag }}</span>
        }
      </div>
    }
  </lodb-entity-card>`,
  host: { class: 'block h-full' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionCard {
  readonly card = input.required<Card>();
  /** Root-relative URL of the champion's page (catalogueHref). */
  readonly href = input.required<string>();
  readonly locale = input.required<string>();
  /** Above the fold: its icon loads at once. */
  readonly eager = input(false, { transform: booleanAttribute });

  protected readonly title = computed(() => capitalize(this.card().title, this.locale()));
}
