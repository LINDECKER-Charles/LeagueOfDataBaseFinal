import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Chip } from '../../../ui/controls/chip';
import { Frame } from '../../../ui/surfaces/frame';

/**
 * A block of a panel in the Hextech frame, the legacy `.card`: headed by a bold `h2` under
 * the page's `h1`, with an optional chip at the end of its head that says what it counts, or
 * a projected chip link ("Détail →"), which stays there on a phone while the title wraps.
 * Without a heading it simply frames a table.
 */
@Component({
  selector: 'lodb-admin-card',
  imports: [Chip],
  hostDirectives: [Frame],
  template: `
    @if (heading()) {
      <div class="mb-[1.15rem] flex items-baseline justify-between gap-4">
        <h2 [class]="headingClass">
          {{ heading() }}
        </h2>
        @if (chip()) {
          <span lodbChip>{{ chip() }}</span>
        }
        <ng-content select="[lodbCardHead]" />
      </div>
    }
    <ng-content />
  `,
  host: { class: 'block min-w-0 px-[1.6rem] py-6' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCard {
  /** The heading, translated. */
  readonly heading = input<string>('');
  /** What the card counts, translated: "vues · visiteurs". */
  readonly chip = input<string>('');

  // Spelled out whole for the Tailwind scanner: the legacy card heading, bold and tracked.
  protected readonly headingClass =
    'font-beaufort text-[1.05rem] font-bold tracking-[0.08em] text-gold-bright uppercase';
}
