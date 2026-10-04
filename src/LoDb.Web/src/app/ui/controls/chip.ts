import { Directive, computed, input } from '@angular/core';
import { CHIP_TONES, type ChipTone } from './chip-tones';

/** Small uppercase mono label on any inline element: `<span lodbChip="live">GET</span>`. */
@Directive({
  selector: '[lodbChip]',
  host: { '[class]': 'toneClass()' },
})
export class Chip {
  /** Tone of the chip; the bare attribute gives the muted tone. */
  readonly lodbChip = input<ChipTone, ChipTone | ''>('muted', {
    transform: (tone) => tone || 'muted',
  });

  protected readonly toneClass = computed(() => CHIP_TONES[this.lodbChip()]);
}
