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
import type { ImageVariant } from '../../core/api/generated/models/image-variant';
import type { ImageState } from './image-state';
import { imageStateOf } from './image-state-of';
import { srcsetOf } from './variants/srcset-of';
import { webpSource } from './webp-source';

/**
 * Codex image in a `<picture>` with its WebP twin, drawn by foundation/images.css while its
 * bytes are in flight (sweep) and when they never arrive (neutral mark, box kept). Without a
 * source it renders the initials box, for an image known to be absent. The size classes go
 * on the host, the width and height reserve the box before the bytes arrive.
 *
 * The WebP candidates are the PNG's twin, or the stored variants of an art the API lists,
 * picked with `sizes`; the source stays the fallback of a browser without WebP.
 * `eager` loads an image above the fold at once; `priority` marks the page's one LCP
 * candidate, fetched first: several images at high priority would only compete.
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
  /** Above-the-fold image: loaded at once. */
  readonly eager = input(false, { transform: booleanAttribute });
  /** The page's LCP candidate: loaded at once and at high priority. One per page. */
  readonly priority = input(false, { transform: booleanAttribute });
  /** Stored WebP variants of the art, in place of the PNG's twin; none for most images. */
  readonly variants = input<readonly ImageVariant[]>([]);
  /** The slot the image fills, for the browser to pick among its variants. */
  readonly sizes = input<string | null>(null);
  /** Classes of the `<img>` itself, such as its object fit. */
  readonly imgClass = input('');

  protected readonly webp = computed(() => {
    const source = this.src();
    return srcsetOf(this.variants()) ?? (source === null ? null : webpSource(source));
  });
  protected readonly loading = computed(() => (this.eager() || this.priority() ? 'eager' : 'lazy'));
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
