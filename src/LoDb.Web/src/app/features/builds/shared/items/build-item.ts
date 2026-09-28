import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import { translateSignal } from '@jsverse/transloco';
import type { ItemView } from '../../../../core/api/generated/models/item-view';
import { Image } from '../../../../ui/media/image';
import { imageSource } from '../media/image-source';
import { initialsOf } from '../media/initials-of';

const FULL_SIZE = 48;
const EXCERPT_SIZE = 32;

/**
 * An item of a purchase order, as a tile: its image named by the item, or its first letters
 * when the patch has no image of it. A ghost, which the patch lacks, stays in its place,
 * dimmed and titled as unavailable; its id stands for its name. `excerpt` draws the smaller
 * tile of the trends, decorative there.
 */
@Component({
  selector: 'lodb-build-item',
  imports: [Image],
  template: `
    <lodb-image
      class="size-full"
      [src]="source()"
      [alt]="excerpt() ? '' : item().name"
      [width]="size()"
      [height]="size()"
      [initials]="initials()"
      imgClass="object-cover"
    />
  `,
  styles: `
    @layer components {
      :host {
        display: block;
        inline-size: 3rem;
        block-size: 3rem;
        border: 1px solid color-mix(in srgb, var(--color-gold) 40%, transparent);
        background: var(--color-void);
      }
      :host(.build-item--excerpt) {
        inline-size: 2rem;
        block-size: 2rem;
      }
      :host(.forge-ghost) {
        opacity: 0.45;
        filter: saturate(0.2);
      }
    }
  `,
  host: {
    class: 'bshare-item',
    '[class.build-item--excerpt]': 'excerpt()',
    '[class.forge-ghost]': 'item().missing',
    '[attr.title]': 'title()',
    '[attr.data-item-id]': 'item().id',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildItem {
  readonly item = input.required<ItemView>();
  readonly excerpt = input(false, { transform: booleanAttribute });

  private readonly missing = translateSignal('build.show.missing');

  protected readonly source = computed(() => imageSource(this.item().image));
  protected readonly initials = computed(() => initialsOf(this.item().name));
  protected readonly size = computed(() => (this.excerpt() ? EXCERPT_SIZE : FULL_SIZE));
  protected readonly title = computed(() =>
    this.item().missing ? this.missing() : this.item().name,
  );
}
