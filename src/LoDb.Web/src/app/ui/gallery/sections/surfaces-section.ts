import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Image } from '../../media/image';
import { Card } from '../../surfaces/card';
import { Frame } from '../../surfaces/frame';
import { Skeleton } from '../../surfaces/skeleton';

// An inline image, so the loaded state shows without any asset; a PNG path gets a WebP twin.
const SAMPLE_ART =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'%3E" +
  "%3Crect width='64' height='64' fill='%23091428'/%3E%3Cpath d='M32 8 56 32 32 56 8 32z'" +
  " fill='none' stroke='%23c8aa6e' stroke-width='3'/%3E%3C/svg%3E";

/** Frames, cards, skeletons and the image lifecycle. */
@Component({
  selector: 'lodb-gallery-surfaces',
  imports: [Card, Frame, Image, Skeleton],
  templateUrl: './surfaces-section.html',
  host: { class: 'block scroll-mt-20' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SurfacesSection {
  protected readonly art = SAMPLE_ART;
}
