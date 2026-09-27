import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { FigureFormat } from '../format/figure';
import { FigurePipe } from '../format/figure-pipe';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/** A ranked entry: a page, a browser, a family of objects. */
export interface RankRow {
  readonly name: string;
  readonly value: number;
}

const PERCENT = 100;
// Spelled out whole for the Tailwind scanner.
const FILLS = { gold: 'block h-full bg-gold', hex: 'block h-full bg-hex' } as const;

/**
 * Entries ranked by their value, each with a bar sized on the largest of the whole set. Past
 * `limit` rows, the rest folds behind a toggle, and the bars keep the scale of every row.
 */
@Component({
  selector: 'lodb-rank-list',
  imports: [FigurePipe, AdminTextPipe],
  template: `
    @if (rows().length === 0) {
      <p class="text-sm text-text-dim">{{ empty() | adminText }}</p>
    } @else {
      <ol class="space-y-2.5">
        @for (row of shown(); track $index) {
          <li>
            <div class="flex items-baseline justify-between gap-3 text-sm">
              <span class="min-w-0 truncate text-text" [title]="row.name">{{ row.name }}</span>
              <span class="shrink-0 font-mono text-xs text-text-muted">
                {{ row.value | figure: format() }}
              </span>
            </div>
            <span class="mt-1 block h-1 bg-gold-deep/25" aria-hidden="true">
              <span [class]="fill()" [style.inline-size.%]="share(row.value)"></span>
            </span>
          </li>
        }
      </ol>
      @if (folded() > 0) {
        <button
          type="button"
          class="nav-link mt-3 cursor-pointer text-xs text-gold"
          [attr.aria-expanded]="open()"
          (click)="open.set(!open())"
        >
          {{
            open()
              ? ('admin.rank.less' | adminText)
              : ('admin.rank.more' | adminText: { count: folded() })
          }}
        </button>
      }
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RankList {
  readonly rows = input.required<readonly RankRow[]>();
  readonly format = input<FigureFormat>('compact');
  readonly tone = input<keyof typeof FILLS>('gold');
  /** Rows shown before the toggle; every row when 0. */
  readonly limit = input(0);
  /** The translation key of what an empty ranking says. */
  readonly empty = input('admin.common.no_data');

  protected readonly open = signal(false);
  protected readonly fill = computed(() => FILLS[this.tone()]);
  protected readonly folded = computed(() =>
    this.limit() > 0 ? Math.max(0, this.rows().length - this.limit()) : 0,
  );
  protected readonly shown = computed(() =>
    this.open() || this.folded() === 0 ? this.rows() : this.rows().slice(0, this.limit()),
  );
  private readonly max = computed(() => Math.max(0, ...this.rows().map((row) => row.value)));

  protected share(value: number): number {
    const max = this.max();
    return max > 0 ? (value / max) * PERCENT : 0;
  }
}
