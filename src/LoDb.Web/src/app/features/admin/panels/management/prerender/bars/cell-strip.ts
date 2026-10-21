import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { stripGradient } from './strip-gradient';

/**
 * The cells of a patch in one thin strip, a segment per locale in the order of the site:
 * one element per row of a long list, where a matrix would draw 21 (§ 4.4). One image to
 * assistive technology, named by `label`: « 12 langues complètes sur 21 ».
 */
@Component({
  selector: 'lodb-cell-strip',
  template: `<span
    class="strip"
    role="img"
    [attr.aria-label]="label()"
    [style.background]="gradient()"
  ></span>`,
  styles: `
    .strip {
      display: block;
      block-size: 0.5rem;
      inline-size: 100%;
      background-color: var(--color-track);
    }
    :host-context([dir='rtl']) .strip {
      transform: scaleX(-1);
    }
  `,
  host: { class: 'block min-w-16' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CellStrip {
  readonly cells = input.required<string>();
  readonly label = input.required<string>();

  protected readonly gradient = computed(() => stripGradient(this.cells()));
}
