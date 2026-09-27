import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { Edition } from '../../core/api/generated/models/edition';

/**
 * The chip of each size, spelled out whole for the Tailwind scanner: compact on cards and in
 * the pager; regular, the chip's own size, after the eyebrow of a detail page's hero, where
 * its margin keeps it apart from the version.
 */
const BADGE_CLASSES = {
  compact: 'hx-chip-hex shrink-0 px-1.5 py-0.5 text-[9px]',
  regular: 'hx-chip-hex shrink-0 ms-3',
} as const;

/**
 * The mark of an entry of the LoL Classic edition. The current game is the default and
 * stays unmarked, so the badge reads "this one is the classic twin".
 */
@Component({
  selector: 'lodb-edition-badge',
  imports: [TranslocoPipe],
  template: `@if (edition() === 'classic') {
    <span [class]="classes[size()]" [title]="'edition.classic_hint' | transloco">
      {{ 'edition.classic' | transloco }}
    </span>
  }`,
  host: { class: 'contents' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditionBadge {
  readonly edition = input<Edition | null | undefined>('modern');
  readonly size = input<keyof typeof BADGE_CLASSES>('compact');

  protected readonly classes = BADGE_CLASSES;
}
