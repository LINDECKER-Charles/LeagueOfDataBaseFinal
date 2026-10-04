import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SKELETON_SHAPES, type SkeletonShape } from './skeleton-shapes';

/**
 * Placeholder of content in flight, typically a `@defer` placeholder: it reserves the size of
 * what arrives so the arrival shifts nothing. Hidden from assistive technology, which is told
 * about loading by the region that owns it (`aria-busy`).
 */
@Component({
  selector: 'lodb-skeleton',
  template: '',
  host: { '[class]': 'shapeClass()', 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Skeleton {
  readonly shape = input<SkeletonShape>('line');

  protected readonly shapeClass = computed(() => SKELETON_SHAPES[this.shape()]);
}
