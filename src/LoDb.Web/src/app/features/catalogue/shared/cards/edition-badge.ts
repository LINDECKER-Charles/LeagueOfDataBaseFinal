import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { Edition } from '../../../../core/api/generated/models/edition';

/**
 * The mark of an entry of the LoL Classic edition. The current game is the default and
 * stays unmarked, so the badge reads "this one is the classic twin".
 */
@Component({
  selector: 'lodb-edition-badge',
  imports: [TranslocoPipe],
  template: `@if (edition() === 'classic') {
    <span
      class="hx-chip-hex shrink-0 px-1.5 py-0.5 text-[9px]"
      [title]="'edition.classic_hint' | transloco"
    >
      {{ 'edition.classic' | transloco }}
    </span>
  }`,
  host: { class: 'contents' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditionBadge {
  readonly edition = input<Edition | null | undefined>('modern');
}
