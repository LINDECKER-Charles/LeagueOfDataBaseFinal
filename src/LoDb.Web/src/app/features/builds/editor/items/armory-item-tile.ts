import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import { Image } from '../../../../ui/media/image';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';

/**
 * An item of the armory's grid, as a button: its icon, badged with how many the step already
 * holds, its name and its price. A press asks the armory to add it to the step.
 */
@Component({
  selector: 'lodb-armory-item-tile',
  imports: [Image, TranslocoPipe],
  template: `
    <button
      type="button"
      class="armory-item"
      [class.armory-item--placed]="count() > 0"
      [disabled]="disabled()"
      [title]="item().name"
      (click)="picked.emit()"
    >
      <span class="armory-item__icon">
        <lodb-image
          class="size-full"
          imgClass="object-cover"
          [src]="imageSource(item().image)"
          [width]="48"
          [height]="48"
          [initials]="initialsOf(item().name)"
        />
        @if (count() > 0) {
          <span
            class="armory-item__badge"
            [title]="'build.editor.armory.in_step' | transloco: { count: count() }"
          >
            {{ count() }}
          </span>
        }
      </span>
      <span class="armory-item__name">{{ item().name }}</span>
      <span class="armory-item__gold">{{ item().gold }} ◆</span>
    </button>
  `,
  styleUrl: './armory-item-tile.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArmoryItemTile {
  readonly item = input.required<ItemOption>();
  /** How many of the item the step already holds: the same item may be bought twice. */
  readonly count = input(0);
  /** A full step takes no more items. */
  readonly disabled = input(false);
  readonly picked = output();

  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
}
