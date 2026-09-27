import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Chip } from '../../../ui/controls/chip';
import { Frame } from '../../../ui/surfaces/frame';

/**
 * A block of a panel in the Hextech frame, headed by an `h2` under the page's `h1`, with an
 * optional chip that says what it counts; the header's end takes a projected link.
 */
@Component({
  selector: 'lodb-admin-card',
  imports: [Chip],
  hostDirectives: [Frame],
  template: `
    @if (heading()) {
      <div class="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h2 class="font-beaufort text-sm tracking-[0.18em] text-gold-bright uppercase">
          {{ heading() }}
        </h2>
        @if (chip()) {
          <span lodbChip>{{ chip() }}</span>
        }
        <ng-content select="[lodbCardLink]" />
      </div>
    }
    <ng-content />
  `,
  host: { class: 'block min-w-0 p-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCard {
  /** The heading, translated. */
  readonly heading = input<string>('');
  /** What the card counts, translated: "views · visitors". */
  readonly chip = input<string>('');
}
