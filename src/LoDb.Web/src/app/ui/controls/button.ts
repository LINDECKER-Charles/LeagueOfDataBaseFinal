import { Directive, computed, input } from '@angular/core';
import { BUTTON_SIZES, type ButtonSize } from './button-sizes';
import { BUTTON_TONES, type ButtonTone } from './button-tones';

/**
 * Hextech button look on a native `<button>` or `<a>`, which keeps its own semantics,
 * keyboard handling and form behaviour: `<button lodbButton="ghost">`, compact with
 * `lodbButtonSize="small"`. Thumb-sized on coarse pointers (primitives/controls.css).
 */
@Directive({
  selector: 'button[lodbButton], a[lodbButton]',
  host: { '[class]': 'lookClass()' },
})
export class Button {
  /** Tone of the button; the bare attribute gives the primary tone. */
  readonly lodbButton = input<ButtonTone, ButtonTone | ''>('primary', {
    transform: (tone) => tone || 'primary',
  });
  /** Size of the button; the tone's own when left out. */
  readonly lodbButtonSize = input<ButtonSize>('default');

  protected readonly lookClass = computed(() =>
    [BUTTON_TONES[this.lodbButton()], BUTTON_SIZES[this.lodbButtonSize()]].join(' ').trim(),
  );
}
