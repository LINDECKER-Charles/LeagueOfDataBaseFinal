import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import { RouterLink, type UrlTree } from '@angular/router';
import type { CatalogImage } from '../../core/api/generated/models/catalog-image';
import type { Edition } from '../../core/api/generated/models/edition';
import { FRAME_STYLES } from '../surfaces/frame-styles';
import { CatalogueImage } from './catalogue-image';
import { EditionBadge } from './edition-badge';

/** Box of the card's icon, in CSS pixels. */
const ICON_SIZE = 56;
// Spelled out whole for the Tailwind scanner. The art zooms inside its box on hover; a
// rune path's mark sits at 72% of a round box, the transform composing with the hover scale.
const ZOOM =
  'transition-transform duration-500 group-hover:scale-110 motion-reduce:transition-none';
const IMG_CLASSES = {
  cover: `object-cover ${ZOOM}`,
  contain: `object-contain ${ZOOM}`,
  mark: `object-cover [transform:scale(0.72)] ${ZOOM}`,
} as const;
const MARK_BOX = 'rounded-full shadow-[inset_0_0_0_1px_var(--hx-ring-accent-2)]';

/**
 * The card shell every catalogue list shares: a link holding the icon, the name, a caption
 * and the edition mark; the page projects the rest of the card (tags, stats) below it.
 */
@Component({
  selector: 'lodb-entity-card',
  imports: [CatalogueImage, EditionBadge, RouterLink],
  template: `<a [routerLink]="href()" class="flex items-center gap-3 px-4 pt-4 pb-3">
      <lodb-catalogue-image
        class="size-14 border border-gold-deep/50 bg-void"
        [class]="fit() === 'mark' ? markBox : ''"
        [image]="image()"
        [name]="name()"
        [size]="iconSize"
        [eager]="eager()"
        [imgClass]="imgClass()"
      />
      <div class="min-w-0">
        <h3
          class="truncate font-beaufort text-[15px] tracking-wide text-text transition-colors
            group-hover:text-gold-bright"
          [title]="name()"
        >
          {{ name() }}
        </h3>
        <div class="flex min-w-0 items-center gap-2">
          @if (caption()) {
            <p class="truncate font-mono text-xs text-text-muted" [title]="caption()">
              {{ caption() }}
            </p>
          }
          <lodb-edition-badge [edition]="edition()" />
        </div>
      </div>
    </a>
    <ng-content />`,
  host: { class: `${FRAME_STYLES.interactive} group flex h-full flex-col overflow-hidden` },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntityCard {
  /**
   * The entry's page (injectCatalogueLink): a UrlTree, since a string would reach the router
   * with the `?lang=` it carries escaped (`%3F`).
   */
  readonly href = input.required<UrlTree>();
  readonly image = input.required<CatalogImage>();
  readonly name = input.required<string>();
  /** One line under the name: a champion's title, an item's price. */
  readonly caption = input<string | null>(null);
  readonly edition = input<Edition | null | undefined>('modern');
  /** How the art fills its box; `mark` is a rune path's glyph, round and inset. */
  readonly fit = input<'cover' | 'contain' | 'mark'>('cover');
  /** Above the fold: loaded at once. */
  readonly eager = input(false, { transform: booleanAttribute });

  protected readonly iconSize = ICON_SIZE;
  protected readonly markBox = MARK_BOX;
  protected readonly imgClass = computed(() => IMG_CLASSES[this.fit()]);
}
