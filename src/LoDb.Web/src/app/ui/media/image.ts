import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterRenderEffect,
  booleanAttribute,
  computed,
  input,
  signal,
  viewChild,
} from '@angular/core';
import type { ImageState } from './image-state';
import { imageStateOf } from './image-state-of';
import { webpSource } from './webp-source';

/**
 * Codex image in a `<picture>` with its WebP twin, drawn by foundation/images.css while its
 * bytes are in flight (sweep) and when they never arrive (neutral mark, box kept). Without a
 * source it renders the initials box, for an image known to be absent. The size classes go
 * on the host, the width and height reserve the box before the bytes arrive.
 *
 * The state is stamped in the browser only, after render: the server-rendered image carries
 * none and renders plainly, so hydration finds the markup it expects.
 */
@Component({
  selector: 'lodb-image',
  templateUrl: './image.html',
  host: { class: 'relative block shrink-0 overflow-hidden' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Image {
  /** Browser-ready URL; null for an image known to be absent. */
  readonly src = input.required<string | null>();
  readonly alt = input('');
  readonly width = input.required<number>();
  readonly height = input.required<number>();
  /** Shown in place of an absent image. */
  readonly initials = input('');
  /** Above-the-fold image: loaded at once and at high priority (the LCP candidate). */
  readonly eager = input(false, { transform: booleanAttribute });
  /** Classes of the `<img>` itself, such as its object fit. */
  readonly imgClass = input('');

  protected readonly webp = computed(() => {
    const source = this.src();
    return source === null ? null : webpSource(source);
  });
  protected readonly state = signal<ImageState | null>(null);
  private readonly img = viewChild<ElementRef<HTMLImageElement>>('img');

  constructor() {
    // Catches what finished before the listeners ran, and restarts on a new source.
    afterRenderEffect(() => {
      this.src();
      const img = this.img()?.nativeElement;
      this.state.set(img ? imageStateOf(img.complete, img.naturalWidth) : null);
    });
  }

  protected settle(state: ImageState): void {
    this.state.set(state);
  }
}
