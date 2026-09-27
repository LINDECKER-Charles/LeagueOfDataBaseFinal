import { Directive, computed, input } from '@angular/core';

/**
 * The tone of a state, as the legacy badges had them: `good` a healthy state, `warn` one to
 * watch, `bad` a failure, `muted` a neutral label.
 */
export type Tone = 'good' | 'warn' | 'bad' | 'muted';

// Spelled out whole for the Tailwind scanner: an outlined mono label, written as it is.
const BADGE = 'inline-flex items-center gap-1 border px-2 py-[0.15rem] font-mono text-[0.72rem]';
const BADGE_TONES: Readonly<Record<Tone, string>> = {
  good: `${BADGE} border-good/50 text-good`,
  warn: `${BADGE} border-gold/28 text-gold`,
  // The lighter red keeps small text readable on the dark canvas.
  bad: `${BADGE} border-bad/50 text-danger-light`,
  muted: `${BADGE} border-gold/28 text-text-muted`,
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
