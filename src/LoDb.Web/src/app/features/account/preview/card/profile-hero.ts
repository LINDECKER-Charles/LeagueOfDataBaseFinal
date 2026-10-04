import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { PublicProfile } from '../../../../core/api/generated/models/public-profile';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { Image } from '../../../../ui/media/image';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { displayName } from '../../shared/text/display-name';
import { memberSince } from '../../shared/text/member-since';
import { heroOrbsOf } from './hero-orbs-of';
import { SupporterBadge } from './supporter-badge';

/**
 * The hero of a public card: the favorite skin's splash, else the champion's, full bleed
 * under a scrim; the other favorites drifting as orbs; the name, the supporter's seal, the
 * day the account joined and the skin's name. The public page has its twin, since a feature
 * cannot import another's.
 */
@Component({
  selector: 'lodb-profile-hero',
  imports: [Backdrop, Image, SupporterBadge, TranslocoPipe],
  templateUrl: './profile-hero.html',
  styleUrl: './hero.css',
  host: {
    class: 'profile-hero',
    '[class.profile-hero--art]': 'profile().backdrop !== null',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileHero {
  private readonly page = inject(PageDirection);

  readonly profile = input.required<PublicProfile>();

  protected readonly name = computed(() =>
    displayName(this.profile().username, this.profile().riotTagline),
  );
  protected readonly since = computed(() =>
    memberSince(this.profile().memberSince, this.page.locale()),
  );
  protected readonly orbs = computed(() => heroOrbsOf(this.profile()));
  private readonly art = viewChild<ElementRef<HTMLImageElement>>('art');

  constructor() {
    // A splash that failed before the page came alive fired its error unheard.
    afterNextRender(() => {
      const art = this.art()?.nativeElement;
      if (art?.complete && art.naturalWidth === 0) {
        this.fallBack(art);
      }
    });
  }

  /** Swaps the centered splash that failed for the wide one, once. */
  protected fallBack(art: HTMLImageElement): void {
    const fallback = this.profile().backdrop?.fallback;
    if (fallback !== undefined && art.getAttribute('src') !== fallback) {
      art.src = fallback;
    }
  }
}
