import { Directive, computed, input } from '@angular/core';
import { FRAME_STYLES, type FrameStyle } from './frame-styles';

/** Hextech frame on any block: `<section lodbFrame="ornate">`. */
@Directive({
  selector: '[lodbFrame]',
  host: { '[class]': 'styleClass()' },
})
export class Frame {
  /** Style of the frame; the bare attribute gives the plain panel. */
  readonly lodbFrame = input<FrameStyle, FrameStyle | ''>('plain', {
    transform: (style) => style || 'plain',
  });

  protected readonly styleClass = computed(() => FRAME_STYLES[this.lodbFrame()]);
}
