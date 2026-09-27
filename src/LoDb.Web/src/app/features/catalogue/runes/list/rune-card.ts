import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { RuneCard as RuneCardModel } from '../../../../core/api/generated/models/rune-card';
import type { PageContext } from '../../../../core/context/page-context';
import { FRAME_STYLES } from '../../../../ui/surfaces/frame-styles';
import { injectCatalogueLink } from '../../shared/codex/links/inject-catalogue-link';
import { RichText } from '../../shared/codex/rich-text/rich-text';
import { CatalogueImage } from '../../../../ui/cards/catalogue-image';

/** Box of the rune's icon, in CSS pixels, as lodb-entity-card draws it. */
const ICON_SIZE = 56;

/**
 * A rune of the list: lodb-entity-card's face, linking into its path's page at the rune's own
 * card, since runes have no page; its description sits under the face.
 */
@Component({
  selector: 'lodb-rune-card',
  imports: [CatalogueImage, RichText, RouterLink],
  template: `<a [routerLink]="link()" class="flex items-center gap-3 px-4 pt-4 pb-3">
      <lodb-catalogue-image
        class="size-14 rounded-full border border-gold-deep/50 bg-void"
        [image]="card().image"
        [name]="card().name"
        [size]="iconSize"
        [eager]="eager()"
        imgClass="object-contain"
      />
      <div class="min-w-0">
        <h3
          class="truncate font-beaufort text-[15px] tracking-wide text-text transition-colors group-hover:text-gold-bright"
          [title]="card().name"
        >
          {{ card().name }}
        </h3>
        <p class="truncate font-mono text-xs text-text-muted" [title]="caption()">
          {{ caption() }}
        </p>
      </div>
    </a>
    @if (card().shortDesc) {
      <lodb-rich-text
        class="px-4 pb-4 text-[13px] leading-snug text-text-muted"
        [text]="card().shortDesc"
      />
    }`,
  host: { class: `${FRAME_STYLES.interactive} group flex h-full flex-col overflow-hidden` },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneCard {
  readonly card = input.required<RuneCardModel>();
  readonly context = input.required<PageContext>();
  /** Its path and row, such as "Precision · Keystone". */
  readonly caption = input.required<string>();
  /** Above the fold: loaded at once. */
  readonly eager = input(false, { transform: booleanAttribute });

  private readonly linkOf = injectCatalogueLink();

  protected readonly iconSize = ICON_SIZE;
  protected readonly link = computed(() =>
    this.linkOf(this.context(), this.card().canonicalPath, `rune-${this.card().key}`),
  );
}
