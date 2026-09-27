import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Tone } from './badge';

// Spelled out whole for the Tailwind scanner: the hairline over the figure.
const ACCENTS: Readonly<Record<Tone, string>> = {
  gold: 'bg-gold',
  hex: 'bg-hex',
  good: 'bg-hex',
  warn: 'bg-gold-rich',
  bad: 'bg-danger',
  muted: 'bg-gold-deep',
};

/**
 * A key figure of a panel: its label, its value, a line under it, and a sparkline or a set
 * of badges projected below. The hairline on top says its tone at a glance.
 */
@Component({
  selector: 'lodb-kpi',
  template: `
    <span [class]="accent()" aria-hidden="true"></span>
    <span class="eyebrow block">{{ label() }}</span>
    <span class="mt-1 block font-beaufort text-2xl text-gold-bright">{{ value() }}</span>
    @if (sub()) {
      <span class="mt-0.5 block text-xs text-text-muted">{{ sub() }}</span>
    }
    <ng-content />
  `,
  host: { class: 'stat-cell block min-w-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Kpi {
  /** The label, translated. */
  readonly label = input.required<string>();
  /** The value, written. */
  readonly value = input.required<string | number>();
  readonly sub = input<string | null>(null);
  readonly tone = input<Tone>('gold');

  protected readonly accent = computed(() => `mb-2 block h-0.5 w-8 ${ACCENTS[this.tone()]}`);
}
