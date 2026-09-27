import { Directive, computed, input } from '@angular/core';
/**
 * The tone of a figure or a state: `gold` the default, `hex` the live cyan, `good` a healthy
 * state, `warn` one to watch, `bad` a failure, `muted` a neutral label.
 */
export type Tone = 'gold' | 'hex' | 'good' | 'warn' | 'bad' | 'muted';

// Spelled out whole for the Tailwind scanner. The chip's frame, in the colour of the tone.
const CHIP = 'inline-flex items-center border px-2.5 py-1 font-mono text-[11px] tracking-wider';
const BADGE_TONES: Readonly<Record<Tone, string>> = {
  gold: `${CHIP} border-gold/60 bg-gold/10 text-gold uppercase`,
  hex: 'hx-chip-hex',
  good: 'hx-chip-hex',
  warn: `${CHIP} border-gold-rich/70 bg-gold-rich/10 text-gold-light uppercase`,
  bad: `${CHIP} border-danger/70 bg-danger/10 text-danger-light uppercase`,
  muted: 'hx-chip',
};

/** A state as a small label: `<span lodbBadge="bad">banni</span>`. */
@Directive({
  selector: '[lodbBadge]',
  host: { '[class]': 'toneClass()' },
})
export class Badge {
  readonly lodbBadge = input<Tone, Tone | ''>('muted', { transform: (tone) => tone || 'muted' });

  protected readonly toneClass = computed(() => BADGE_TONES[this.lodbBadge()]);
}
