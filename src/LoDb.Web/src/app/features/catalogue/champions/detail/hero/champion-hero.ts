import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterRenderEffect,
  computed,
  input,
  linkedSignal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChampionDetails } from '../../../../../core/api/generated/models/champion-details';
import type { PageContext } from '../../../../../core/context/page-context';
import { imageStateOf } from '../../../../../ui/media/image-state-of';
import { srcsetOf } from '../../../../../ui/media/variants/srcset-of';
import { capitalize } from '../../text/capitalize';
import { ratingGaugesOf } from './rating-gauges-of';

/** Diamonds of a rating gauge: Riot rates from 0 to 10. */
const GAUGE_STEPS = Array.from({ length: 10 }, (_, index) => index);
// Data Dragon's centered splash: 1280 by 720.
const ART_WIDTH = 1280;
const ART_HEIGHT = 720;
/**
 * The width the art is drawn at: the hero's full width, or wider when its height (62vh,
 * between 26 and 46rem, as its classes set it) asks a 16:9 art to cover more, as on a phone.
 */
const ART_SIZES = 'max(100vw, calc(clamp(26rem, 62vh, 46rem) * 16 / 9))';

/**
 * The cinematic hero of a champion page, the page's LCP candidate: the centered splash as the
 * WebP variants the site stores for the latest patch, at the width the screen draws it, else
 * hotlinked; then the classic splash for a champion without one. Then the name, the title,
 * the roles, the resource and Riot's four ratings as gauges of ten diamonds.
 */
@Component({
  selector: 'lodb-champion-hero',
  imports: [TranslocoPipe],
  templateUrl: './champion-hero.html',
  styleUrl: './champion-hero.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionHero {
  readonly details = input.required<ChampionDetails>();
  readonly context = input.required<PageContext>();

  protected readonly steps = GAUGE_STEPS;
  protected readonly artWidth = ART_WIDTH;
  protected readonly artHeight = ART_HEIGHT;
  protected readonly sizes = ART_SIZES;
  protected readonly profile = computed(() => this.details().profile);
  protected readonly title = computed(() =>
    capitalize(this.profile().title, this.context().locale),
  );
  protected readonly gauges = computed(() => ratingGaugesOf(this.profile().ratings));
  protected readonly art = linkedSignal(() => this.details().art.centered);
  protected readonly variants = linkedSignal(() => srcsetOf(this.details().centeredArtVariants));
  private readonly splash = viewChild<ElementRef<HTMLImageElement>>('splash');

  constructor() {
    // An image that failed before hydration fired its error unheard: the DOM still tells.
    afterRenderEffect(() => {
      const img = this.splash()?.nativeElement;
      if (img && imageStateOf(img.complete, img.naturalWidth) === 'error') {
        this.fallBack();
      }
    });
  }

  /** One step down at each failure: the stored variants, the hotlinked art, the splash. */
  protected fallBack(): void {
    if (this.variants() !== null) {
      this.variants.set(null);
      return;
    }
    this.art.set(this.details().art.splash);
  }
}
