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
import { capitalize } from '../../text/capitalize';
import { ratingGaugesOf } from './rating-gauges-of';

/** Diamonds of a rating gauge: Riot rates from 0 to 10. */
const GAUGE_STEPS = Array.from({ length: 10 }, (_, index) => index);

/**
 * The cinematic hero of a champion page: the centered splash, hotlinked, falling back to the
 * classic splash for a champion without one; the name, the title, the roles, the resource
 * and Riot's four ratings as gauges of ten diamonds.
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
  protected readonly profile = computed(() => this.details().profile);
  protected readonly title = computed(() =>
    capitalize(this.profile().title, this.context().locale),
  );
  protected readonly gauges = computed(() => ratingGaugesOf(this.profile().ratings));
  protected readonly art = linkedSignal(() => this.details().art.centered);
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

  protected fallBack(): void {
    this.art.set(this.details().art.splash);
  }
}
