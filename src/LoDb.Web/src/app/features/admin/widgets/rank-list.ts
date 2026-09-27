import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { FigureFormat } from '../format/figure';
import { FigurePipe } from '../format/figure-pipe';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import { foldRows } from './fold-rows';
import { FoldToggle } from './fold-toggle';

/** A ranked entry: a page, a browser, a family of objects. */
export interface RankRow {
  readonly name: string;
  readonly value: number;
}

const PERCENT = 100;
// Spelled out whole for the Tailwind scanner: the legacy gradients, gold or cyan.
const FILLS = {
  gold: 'block h-full bg-linear-to-r from-gold-deep to-gold',
  hex: 'block h-full bg-linear-to-r from-hex-deep to-hex',
} as const;
// A row: its name and its value on a line, its bar under them.
const ROW = 'grid grid-cols-[minmax(6rem,1fr)_auto] items-center gap-x-[0.9rem] gap-y-1.5';

/**
 * Entries ranked by their value, each with a bar sized on the largest of the whole set. Past
 * `limit` rows, the rest folds behind a toggle, and the bars keep the scale of every row.
 */
@Component({
  selector: 'lodb-rank-list',
  imports: [FigurePipe, FoldToggle, AdminTextPipe],
  template: `
    @if (rows().length === 0) {
      <p class="text-text-dim">{{ empty() | adminText }}</p>
    } @else {
      <ol class="flex flex-col gap-[0.7rem]">
        @for (entry of fold.shown(); track $index) {
          <li [class]="rowClass">
            <span class="truncate text-[0.86rem] text-text" [title]="entry.name">{{
              entry.name
            }}</span>
            <span class="font-mono text-[0.82rem] text-gold-bright tabular-nums">
              {{ entry.value | figure: format() }}
            </span>
            <span class="col-span-full block h-1.5 overflow-hidden bg-track" aria-hidden="true">
              <span [class]="fill()" [style.inline-size.%]="share(entry.value)"></span>
            </span>
          </li>
        }
      </ol>
      <lodb-fold-toggle [folded]="fold.folded()" [(open)]="fold.open" />
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

  protected readonly rowClass = ROW;
  protected readonly fill = computed(() => FILLS[this.tone()]);
  protected readonly fold = foldRows(
    () => this.rows(),
    () => this.limit(),
  );
  private readonly max = computed(() => Math.max(0, ...this.rows().map((row) => row.value)));

  protected share(value: number): number {
    const max = this.max();
    return max > 0 ? (value / max) * PERCENT : 0;
  }
}
