import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  type ElementRef,
  afterNextRender,
  afterRenderEffect,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { AbilityVideo } from '../../../../../core/api/generated/models/ability-video';
import type { ChampionAbility } from '../../../../../core/api/generated/models/champion-ability';
import { CatalogueImage } from '../../../shared/cards/catalogue-image';
import { injectVideoPlayback } from './playback/inject-video-playback';

const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';
/** Box of the icon shown when the ability has no clip, in CSS pixels. */
const ICON_SIZE = 112;

type MediaMode = 'video' | 'poster' | 'idle';

/**
 * The stage of the selected ability: its official looping clip, hotlinked from Riot's CDN,
 * muted until the reader asks for sound, with a click-to-pause surface and a progress bar.
 * The server and the hydration render the clip's poster; the video replaces it once the page
 * has rendered in the browser, unless the reader asked for reduced motion. Without a clip,
 * or when it fails to load, the ability's icon holds the stage.
 */
@Component({
  selector: 'lodb-ability-media',
  imports: [CatalogueImage, TranslocoPipe],
  templateUrl: './ability-media.html',
  styleUrl: './ability-media.css',
  host: {
    '[class.idle]': "mode() === 'idle'",
    '[class.is-paused]': "mode() === 'video' && playback.isPaused()",
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AbilityMedia {
  readonly ability = input.required<ChampionAbility>();
  /** The clip to play; null when the ability has none or it failed. */
  readonly clip = input<AbilityVideo | null>(null);
  /** The clip or its poster failed to load. */
  readonly failed = output();

  protected readonly playback = injectVideoPlayback();
  protected readonly iconSize = ICON_SIZE;
  private readonly mounted = signal(false);
  private readonly reducedMotion = signal(true);
  protected readonly mode = computed<MediaMode>(() => {
    if (this.clip() === null) {
      return 'idle';
    }
    return this.mounted() && !this.reducedMotion() ? 'video' : 'poster';
  });
  // One element per clip: a new clip never reuses the element, and its sources, of the last.
  protected readonly clips = computed(() => {
    const clip = this.clip();
    return this.mode() === 'video' && clip !== null ? [clip] : [];
  });
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');

  constructor() {
    const view = inject(DOCUMENT).defaultView;
    afterNextRender(() => {
      this.reducedMotion.set(view?.matchMedia(REDUCED_MOTION).matches ?? true);
      this.mounted.set(true);
    });
    afterRenderEffect(() => this.playback.adopt(this.video()?.nativeElement ?? null));
  }
}
