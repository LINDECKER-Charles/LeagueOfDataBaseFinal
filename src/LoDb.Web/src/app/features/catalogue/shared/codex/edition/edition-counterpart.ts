import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { CounterpartLink } from '../../../../../core/api/generated/models/counterpart-link';
import type { Edition } from '../../../../../core/api/generated/models/edition';
import type { PageContext } from '../../../../../core/context/page-context';
import { injectCatalogueLink } from '../links/inject-catalogue-link';

/**
 * The edition note of an item or summoner spell page: what a LoL Classic entry is, and the
 * link to its same-named twin in the other game (1004 and 771004, SummonerFlash and
 * SummonerFlash_Jade), found by id by the API. Nothing for a current entry without a twin,
 * and no link when the catalogue does not carry the twin's page.
 */
@Component({
  selector: 'lodb-edition-counterpart',
  imports: [RouterLink, TranslocoPipe],
  template: `@if (isClassic() || twin()) {
    <div
      class="mt-4 flex flex-wrap items-center gap-x-4 gap-y-2"
      [class.justify-center]="centered()"
    >
      @if (isClassic()) {
        <p class="font-mono text-xs text-text-muted">{{ 'edition.classic_notice' | transloco }}</p>
      }
      @if (twin(); as link) {
        <a
          class="inline-flex items-center gap-1.5 font-beaufort text-xs tracking-[0.15em] text-hex uppercase transition-colors hover:text-hex-bright"
          [routerLink]="link.href"
          [attr.data-edition]="link.edition"
        >
          {{ 'edition.counterpart.' + link.edition | transloco }}
          <!-- U+203A mirrors by itself in a right-to-left page: no flip on top of it. -->
          <span aria-hidden="true">›</span>
        </a>
      }
    </div>
  }`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditionCounterpart {
  readonly edition = input.required<Edition>();
  readonly counterpart = input<CounterpartLink | null | undefined>(null);
  readonly context = input.required<PageContext>();
  /** Centred under a centred hero, such as a summoner spell's seal. */
  readonly centered = input(false);

  private readonly linkOf = injectCatalogueLink();

  protected readonly isClassic = computed(() => this.edition() === 'classic');
  protected readonly twin = computed(() => {
    const counterpart = this.counterpart();
    if (!counterpart?.canonicalPath) {
      return null;
    }
    const href = this.linkOf(this.context(), counterpart.canonicalPath);
    return { href, edition: counterpart.edition };
  });
}
