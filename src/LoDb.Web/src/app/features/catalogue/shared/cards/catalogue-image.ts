import { ChangeDetectionStrategy, Component, booleanAttribute, input } from '@angular/core';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import { Image } from '../../../../ui/media/image';
import { initialsOf } from './initials-of';

/**
 * An image of the catalogue as the API describes it: present, drawn by lodb-image; absent,
 * the initials of the name; pending (a cold version still fetching its art), the sweep of
 * data in flight, until the list's one retry brings its URL. The box is the same in all
 * three, so nothing shifts when the art arrives.
 */
@Component({
  selector: 'lodb-catalogue-image',
  imports: [Image],
  template: `@if (image().status === 'pending') {
      <span class="hx-sk block size-full" aria-hidden="true"></span>
    } @else {
      <lodb-image
        class="size-full"
        [src]="image().status === 'present' ? (image().url ?? null) : null"
        [alt]="name()"
        [width]="size()"
        [height]="size()"
        [initials]="initials(name())"
        [eager]="eager()"
        [imgClass]="imgClass()"
      />
    }`,
  host: { class: 'relative block shrink-0 overflow-hidden' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueImage {
  readonly image = input.required<CatalogImage>();
  /** Accessible name of the image, and the source of its initials. */
  readonly name = input.required<string>();
  /** Intrinsic size in CSS pixels, square: it reserves the box. */
  readonly size = input.required<number>();
  readonly eager = input(false, { transform: booleanAttribute });
  readonly imgClass = input('object-cover');

  protected readonly initials = initialsOf;
}
