import { Directive, computed, input } from '@angular/core';
import { BUTTON_TONES, type ButtonTone } from './button-tones';

/**
 * Hextech button look on a native `<button>` or `<a>`, which keeps its own semantics,
 * keyboard handling and form behaviour: `<button lodbButton="ghost">`. Thumb-sized on coarse
 * pointers (primitives/controls.css).
 */
@Directive({
  selector: 'button[lodbButton], a[lodbButton]',
  host: { '[class]': 'toneClass()' },
})
export class Button {
  /** Tone of the button; the bare attribute gives the primary tone. */
  readonly lodbButton = input<ButtonTone, ButtonTone | ''>('primary', {
    transform: (tone) => tone || 'primary',
  });

  protected readonly toneClass = computed(() => BUTTON_TONES[this.lodbButton()]);
}
