import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import { RouterLink, type UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChampionCard as Card } from '../../../../core/api/generated/models/champion-card';
import { initialsOf } from '../../../../ui/cards/initials-of';
import { Image } from '../../../../ui/media/image';
import { FRAME_STYLES } from '../../../../ui/surfaces/frame-styles';
import { capitalize } from '../text/capitalize';

// Data Dragon's loading-screen portraits are 308 by 560: the box is reserved before they land.
const ART_WIDTH = 308;
const ART_HEIGHT = 560;

/**
 * A champion in the list, the whole card a link: its loading-screen portrait, hotlinked from
 * Data Dragon, over a gold weave and its initials, then its name, title, roles, lore teaser
 * and a call to its page.
 *
 * The legacy card meant to lay the patch's square icon over the portrait, but the icon box
 * was `relative` and `absolute` at once, `relative` won, and the icon fell below the art,
 * clipped to a sliver. The card shows what the legacy page shows: the portrait alone.
 */
@Component({
  selector: 'lodb-champion-card',
  imports: [Image, RouterLink, TranslocoPipe],
  template: `<a [routerLink]="href()" [class]="frame">
    <div class="relative aspect-[4/5] overflow-hidden border-b border-gold-deep/50 bg-void">
      <span aria-hidden="true" class="hatch absolute inset-0"></span>
      <span
        aria-hidden="true"
        class="absolute inset-0 grid place-items-center font-beaufort text-6xl text-gold/12"
      >
        {{ initials() }}
      </span>
      <lodb-image
        class="size-full"
        imgClass="object-cover object-top transition-transform duration-500 group-hover:scale-105 motion-reduce:transition-none"
        [src]="card().loadingArt"
        [alt]="card().name"
        [width]="artWidth"
        [height]="artHeight"
        [eager]="eager()"
      />
    </div>
    <div class="flex flex-1 flex-col p-4">
      <h3
        class="truncate font-beaufort text-[15px] tracking-wide text-gold-grad"
        [title]="card().name"
      >
        {{ card().name }}
      </h3>
      @if (title()) {
        <p class="mt-0.5 truncate font-spiegel text-xs text-text-muted">{{ title() }}</p>
      }
      @if (card().tags.length > 0) {
        <div class="mt-2.5 flex flex-wrap gap-1.5">
          @for (tag of card().tags; track tag) {
            <span class="hx-chip">{{ tag }}</span>
          }
        </div>
      }
      @if (card().blurb) {
        <p class="mt-2 line-clamp-2 text-[13px] leading-snug text-text-muted">
          {{ card().blurb }}
        </p>
      }
      <span
        class="mt-auto inline-flex items-center gap-2 pt-3 font-beaufort text-xs tracking-[0.14em] text-gold uppercase transition-colors group-hover:text-hex"
      >
        {{ 'common.detail' | transloco }}
        <svg
          class="size-3.5 transition-transform duration-300 group-hover:translate-x-1 motion-reduce:transition-none rtl:-scale-x-100 rtl:group-hover:-translate-x-1"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="M5 12h14M13 6l6 6-6 6" />
        </svg>
      </span>
    </div>
  </a>`,
  // The weave behind the portrait, while it loads or where it leaves a gap.
  styles: `
    .hatch {
      background-image: repeating-linear-gradient(
        135deg,
        color-mix(in srgb, var(--color-gold) 5%, transparent) 0 2px,
        transparent 2px 10px
      );
    }
  `,
  host: { class: 'block h-full' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionCard {
  readonly card = input.required<Card>();
  /** The champion's page (injectCatalogueLink). */
  readonly href = input.required<UrlTree>();
  readonly locale = input.required<string>();
  /** First row: its portrait loads at once. */
  readonly eager = input(false, { transform: booleanAttribute });

  protected readonly frame = `${FRAME_STYLES.interactive} group flex h-full flex-col overflow-hidden`;
  protected readonly artWidth = ART_WIDTH;
  protected readonly artHeight = ART_HEIGHT;
  protected readonly title = computed(() => capitalize(this.card().title, this.locale()));
  protected readonly initials = computed(() => initialsOf(this.card().name));
}
