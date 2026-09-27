import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The accent of a tile, the legacy's `--accent`: gold and the Hextech cyan, the categorical
 * series (blue, green, red, cyan), and the states `good`, `warn`, `bad`.
 */
export type Accent = 'gold' | 'hex' | 'blue' | 'green' | 'red' | 'cyan' | 'good' | 'warn' | 'bad';

const ACCENT_COLORS: Readonly<Record<Accent, string>> = {
  gold: 'var(--color-gold)',
  hex: 'var(--color-hex)',
  blue: 'var(--color-series-blue)',
  green: 'var(--color-series-green)',
  red: 'var(--color-series-red)',
  cyan: 'var(--color-series-cyan)',
  good: 'var(--color-good)',
  // The legacy `--warn` is the gold itself.
  warn: 'var(--color-gold)',
  bad: 'var(--color-bad)',
};
// Spelled out whole for the Tailwind scanner: the legacy tile, and the border of a link tile.
const TILE = [
  'relative block min-w-0 overflow-hidden border border-gold/28 bg-void/60 px-[1.15rem] py-4',
  'transition-colors',
].join(' ');
const LINK_TILE = `${TILE} hover:border-hex`;

/**
 * A key figure of a panel, the legacy tile: its label, its value and a line under it, a bar
 * of its accent along its start edge, and a sparkline in its end corner. Content projected
 * without a slot (the badges of the services) sits between the label and the line. On an
 * `<a lodbKpi>` it is a link, lit cyan on hover, its line in cyan.
 */
@Component({
  selector: 'lodb-kpi, a[lodbKpi]',
  template: `
    <span
      class="absolute inset-y-0 start-0 w-0.5"
      aria-hidden="true"
      [style.background]="accentColor()"
    ></span>
    <span class="block text-[0.66rem] tracking-[0.14em] text-text-muted uppercase">
      {{ label() }}
    </span>
    @if (value() !== null) {
      <span class="mt-1.5 block font-beaufort text-[1.9rem] leading-[1.1] text-gold-bright">
        {{ value() }}
      </span>
    }
    <ng-content />
    @if (sub()) {
      <span class="mt-1 block text-[0.72rem]" [class]="isLink() ? 'text-hex' : 'text-text-dim'">
        {{ sub() }}
      </span>
    }
    <span class="absolute end-2.5 bottom-2 h-[1.6rem] w-[5.5rem] opacity-75">
      <ng-content select="lodb-sparkline" />
    </span>
  `,
  host: { '[class]': 'tileClass()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Kpi {
  /** The label, translated. */
  readonly label = input.required<string>();
  /** The value, written; none for a tile of badges. */
  readonly value = input<string | number | null>(null);
  readonly sub = input<string | null>(null);
  readonly accent = input<Accent>('gold');
  /** Whether the tile is a link: lit cyan on hover, its line in cyan. */
  readonly isLink = input(false);

  protected readonly accentColor = computed(() => ACCENT_COLORS[this.accent()]);
  protected readonly tileClass = computed(() => (this.isLink() ? LINK_TILE : TILE));
}
