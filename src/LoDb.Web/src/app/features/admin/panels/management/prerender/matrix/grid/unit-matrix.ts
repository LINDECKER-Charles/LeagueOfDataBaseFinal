import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { MatrixTable } from '../table/matrix-table';
import { cellStateOf } from './cell-state';
import { cellTooltip } from './cell-tooltip';
import { type MatrixRow, type MatrixSeason, matrixRows } from './matrix-rows';

/** A version of the matrix: one cell code per locale, and where the worker reaches it. */
export interface MatrixVersion {
  readonly version: string;
  readonly cells: string;
  readonly rank?: number;
}

/** A unit running, or stopped by a pause, and how far it went. */
export interface RunningCell {
  readonly version: string;
  readonly locale: string;
  /** cursor / pagesTotal, 0 to 1; for an r or a z cell. */
  readonly share: number;
}

interface Tooltip {
  readonly text: string;
  readonly start: number;
  readonly top: number;
}

const MATRIX = 'prerender.common.matrix';
// The visitor's choice of view, kept in this browser only.
const VIEW_KEY = 'lodb.admin.prerender.matrix-view';
const TABLE = 'table';
// Two codes too long for a 16px column head, in full in the tooltip.
const SHORT: Readonly<Record<string, string>> = { 'zh-hans': 'zhs', 'zh-hant': 'zht' };
const PERCENT = 100;
const FILLED = 'rz';
// The cells the legend names, a running one half filled: the grid itself is hidden from
// assistive technology, and so is its legend (the table view says it all).
const LEGEND: Readonly<Record<'pass' | 'coverage', readonly string[]>> = {
  pass: ['c', 'r', '.', 'w', 'f'],
  coverage: ['c', 'w', 'p', 'f', '-'],
};
const HALF = 50;

function keyOf(version: string, locale: string): string {
  return `${version}|${locale}`;
}

/**
 * The units of a pass, or the cells of an epoch, version by locale (§ 4.4): a row per version
 * grouped by season, folded but for the seasons where something happens, a column per
 * locale. A cell running fills from the bottom and pulses; one stopped by a pause fills
 * without a halo. One tooltip for the whole grid. The grid is hidden from assistive
 * technology: a sentence per season, the buttons of the seasons and versions, and the table
 * view (« Matrice | Tableau », remembered in this browser) say it all.
 */
@Component({
  selector: 'lodb-unit-matrix',
  imports: [AdminTextPipe, MatrixTable],
  templateUrl: './unit-matrix.html',
  styleUrls: ['./unit-matrix.css', '../cells/cell-states.css'],
  host: { class: 'block min-w-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitMatrix {
  readonly mode = input.required<'pass' | 'coverage'>();
  readonly locales = input.required<readonly string[]>();
  readonly versions = input.required<readonly MatrixVersion[]>();
  readonly running = input<readonly RunningCell[]>([]);
  /** Units the client knows in detail (running, recent, failures), for the tooltip. */
  readonly details = input<readonly PrerenderUnitView[]>([]);
  /** Coverage mode: one hexadecimal mask per locale, by version, for the tooltip. */
  readonly held = input<ReadonlyMap<string, string>>(new Map());
  readonly selected = input<ReadonlySet<string>>(new Set()); // coverage mode
  readonly opened = output<string>(); // pass mode: a version's detail
  readonly toggled = output<string>(); // coverage mode: a version's selection

  private readonly texts = injectAdminText();
  private readonly storage = inject(DOCUMENT).defaultView?.localStorage;
  private readonly folds = signal<ReadonlyMap<string, boolean>>(new Map());

  protected readonly view = signal(this.storedView());
  protected readonly byRank = signal(false);
  protected readonly tooltip = signal<Tooltip | null>(null);
  protected readonly legend = computed(() =>
    LEGEND[this.mode()].map((code) => ({
      code,
      state: cellStateOf(code, this.mode()),
      fill: FILLED.includes(code) ? HALF : null,
    })),
  );
  protected readonly seasons = computed(() =>
    matrixRows(this.versions(), {
      mode: this.mode(),
      folds: this.folds(),
      byRank: this.byRank(),
    }),
  );
  private readonly fills = computed(
    () => new Map(this.running().map((cell) => [keyOf(cell.version, cell.locale), cell.share])),
  );
  private readonly units = computed(
    () => new Map(this.details().map((unit) => [keyOf(unit.version, unit.locale), unit])),
  );

  protected setView(view: 'matrix' | 'table'): void {
    this.view.set(view);
    try {
      this.storage?.setItem(VIEW_KEY, view);
    } catch {
      // Private browsing or storage refused: the choice lasts for this page only.
    }
  }

  protected fold(season: MatrixSeason): void {
    this.folds.update((folds) => new Map(folds).set(season.key, !season.isOpen));
  }

  protected foldAll(isOpen: boolean): void {
    this.folds.set(new Map(this.seasons().map((season) => [season.key, isOpen])));
  }

  protected short(locale: string): string {
    return SHORT[locale] ?? locale;
  }

  /** How far a running or stopped cell fills, from 0 to 100; null for any other. */
  protected fillOf(row: MatrixRow, index: number): number | null {
    if (!FILLED.includes(row.codes[index] ?? '')) {
      return null;
    }
    const share = this.fills().get(keyOf(row.version.version, this.locales()[index] ?? ''));
    return Math.round((share ?? 0) * PERCENT);
  }

  protected seasonLabel(season: MatrixSeason): string {
    if (season.isRanked) {
      return this.texts.text(`${MATRIX}.by_rank`);
    }
    const { major, year } = season;
    return year === null
      ? this.texts.text('prerender.common.seasons.label_bare', { major: season.key })
      : this.texts.text('prerender.common.seasons.label', { major, year });
  }

  protected seasonCounts(season: MatrixSeason): string {
    const { versions, complete, running, failed } = season.counts;
    return this.texts.text(`${MATRIX}.season_counts`, { versions, complete, running, failed });
  }

  protected seasonSummary(season: MatrixSeason): string {
    return this.texts.text(`${MATRIX}.season_summary`, {
      ...season.counts,
      season: this.seasonLabel(season),
    });
  }

  protected versionLabel(row: MatrixRow): string {
    return this.texts.text(`${MATRIX}.version_button`, {
      version: row.version.version,
      complete: figure(row.complete),
      total: row.total,
    });
  }

  protected heat(share: number): string {
    return `color-mix(in srgb, var(--color-good) ${Math.round(share * PERCENT)}%, transparent)`;
  }

  /** The delegated click of a cell: in a pass, the detail of its version. */
  protected click(event: Event): void {
    const version = this.cellOf(event)?.dataset['v'];
    if (version && this.mode() === 'pass') {
      this.opened.emit(version);
    }
  }

  /** The delegated tooltip, placed under the cell from the start of the line. */
  protected hover(event: Event, frame: HTMLElement): void {
    const cell = this.cellOf(event);
    if (!cell) {
      this.tooltip.set(null);
      return;
    }
    const box = cell.getBoundingClientRect();
    const outer = frame.getBoundingClientRect();
    const isRtl = getComputedStyle(frame).direction === 'rtl';
    this.tooltip.set({
      text: this.tooltipOf(cell),
      start: isRtl ? outer.right - box.right : box.left - outer.left + frame.scrollLeft,
      top: box.bottom - outer.top + frame.scrollTop,
    });
  }

  private tooltipOf(cell: HTMLElement): string {
    const version = cell.dataset['v'] ?? '';
    const locale = cell.dataset['l'] ?? '';
    const index = this.locales().indexOf(locale);
    const row = this.versions().find((candidate) => candidate.version === version);
    return cellTooltip(
      {
        mode: this.mode(),
        version,
        locale,
        code: row?.cells[index] ?? '_',
        unit: this.units().get(keyOf(version, locale)),
        rank: row?.rank,
        held: this.held().get(version)?.[index],
      },
      this.texts,
    );
  }

  private cellOf(event: Event): HTMLElement | null {
    const target = event.target instanceof Element ? event.target : null;
    return target?.closest<HTMLElement>('[data-v][data-l]') ?? null;
  }

  private storedView(): 'matrix' | 'table' {
    try {
      return this.storage?.getItem(VIEW_KEY) === TABLE ? 'table' : 'matrix';
    } catch {
      return 'matrix';
    }
  }
}
