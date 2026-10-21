import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { PrerenderPatchRow } from '../../../../../../core/api/generated/models/prerender-patch-row';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../ui/controls/button';
import { Skeleton } from '../../../../../../ui/surfaces/skeleton';
import { figure } from '../../../../format/figure';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminBand } from '../../../../layout/admin-band';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { type BarSegment, StackedBar } from '../bars/stacked-bar';
import { CellStrip } from '../bars/cell-strip';
import { injectPatches } from '../common/scope/inject-patches';
import type { LaunchPreset } from '../common/scope/launch-preset';
import { type PatchSelection, selectionAfter } from '../common/selection/patch-selection';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { UnitMatrix } from '../matrix/grid/unit-matrix';
import { entryPreset } from '../overview/blocks/entry-preset';
import type { LaunchLock } from '../overview/state/launch-lock-of';
import { type CoverageGroup, type StatusFilter, coverageGroups } from './coverage-groups';
import { type SeasonShares, coverageSummaryOf } from './coverage-summary-of';

/** A season of the table, folded or not. */
interface Season extends CoverageGroup {
  readonly isOpen: boolean;
  /** Whether its rows are ticked: all, some or none. */
  readonly ticked: 'all' | 'some' | 'none';
}

const COVERAGE = 'prerender.coverage';
const RETIRED = 'retired';
const STATUSES: readonly StatusFilter[] = ['all', 'complete', 'partial', 'absent', 'failed'];
const VIEWS = ['list', 'matrix'] as const;

/** How the patches show: a table by season, or the matrix of their cells. */
type View = (typeof VIEWS)[number];
const NO_SELECTION: PatchSelection = {
  selected: new Set(),
  anchor: null,
  priority: [],
  extent: null,
};
const GOOD = 'var(--color-good)';
const PARTIAL = 'var(--color-gold)';
const BAD = 'var(--color-bad)';
const TRACK = 'var(--color-track)';

function tickedOf(group: CoverageGroup, selected: ReadonlySet<string>): Season['ticked'] {
  const count = group.rows.filter((row) => selected.has(row.version)).length;
  if (count === 0) {
    return 'none';
  }
  return count === group.rows.length ? 'all' : 'some';
}

/**
 * The « Couverture » tab (§ 5.2): what an epoch holds of each older patch. The epoch read
 * (the served one first), a summary by status and a small bar per season, then the patches
 * by season, the seasons without gap folded, as a list or a matrix. Ticking patches (Shift
 * for a range) offers « Pré-rendre la sélection »; « Compléter les manques » takes every patch
 * with a gap. Both wait while a pass is active, saying why.
 */
@Component({
  selector: 'lodb-coverage-tab',
  imports: [
    AdminBand,
    AdminTextPipe,
    Button,
    CellStrip,
    DecimalBytesPipe,
    FigurePipe,
    Skeleton,
    StackedBar,
    StampPipe,
    UnitMatrix,
  ],
  templateUrl: './coverage-tab.html',
  host: { class: 'flex flex-col gap-4' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CoverageTab {
  readonly report = input.required<PrerenderReport>();
  readonly lock = input.required<LaunchLock | null>();
  readonly launch = output<LaunchPreset>();

  private readonly texts = injectAdminText();
  private readonly folds = signal<ReadonlyMap<string, boolean>>(new Map());

  /** The epoch read; null for the served one. */
  protected readonly epoch = signal<number | null>(null);
  protected readonly patches = injectPatches(this.epoch);
  protected readonly statuses = STATUSES;
  protected readonly query = signal('');
  protected readonly status = signal<StatusFilter>('all');
  protected readonly views = VIEWS;
  protected readonly view = signal<View>('list');
  protected readonly selection = signal<PatchSelection>(NO_SELECTION);
  protected readonly epochs = computed(() =>
    this.report()
      .storage.epochs.filter((volume) => volume.state !== RETIRED)
      .map((volume) => {
        const coverage = volume.coverage;
        const share = coverage?.expectedUnits
          ? figure(coverage.completeUnits / coverage.expectedUnits, 'share')
          : '—';
        const state = this.texts.term('prerender.states', volume.state);
        return {
          epoch: volume.epoch,
          label: this.texts.text(`${COVERAGE}.epoch_option`, { state, epoch: volume.epoch, share }),
        };
      }),
  );
  /** The epoch the coverage on screen is of. */
  protected readonly shownEpoch = computed(
    () => this.patches.value()?.epoch ?? this.epoch() ?? this.report().servedEpoch,
  );
  protected readonly summary = computed(() => {
    const read = this.patches.value();
    return read ? coverageSummaryOf(read) : null;
  });
  protected readonly summaryText = computed(() => {
    const counted = this.summary();
    if (!counted) {
      return '';
    }
    const share = counted.share === null ? '—' : figure(counted.share, 'share');
    return this.texts.text(`${COVERAGE}.summary`, { ...counted, share });
  });
  protected readonly errorText = computed(() => {
    const failure = this.patches.failure();
    const reason = failure
      ? this.texts.text(`state.reasons.${failure.kind}`, { status: failure.status })
      : '';
    return this.texts.text(`${COVERAGE}.error`, { reason });
  });
  protected readonly seasons = computed<Season[]>(() => {
    const filter = { query: this.query(), status: this.status() };
    const selected = this.selection().selected;
    return coverageGroups(this.patches.value()?.patches ?? [], filter).map((group) => ({
      ...group,
      isOpen: this.folds().get(group.key) ?? group.missing > 0,
      ticked: tickedOf(group, selected),
    }));
  });
  protected readonly held = computed(
    () => new Map((this.patches.value()?.patches ?? []).map((row) => [row.version, row.types])),
  );
  // The rows a Shift range runs over, folded seasons included: the order on screen.
  private readonly order = computed(() =>
    this.seasons().flatMap((season) => season.rows.map((row) => row.version)),
  );

  /** Reads the coverage again: the end of a pass, the refresh of the page. */
  reload(): void {
    this.patches.reload();
  }

  protected filter(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected toggle(version: string, event: MouseEvent): void {
    // The selection decides what is ticked, not the browser: a Shift range may untick.
    event.preventDefault();
    const action = event.shiftKey
      ? { kind: 'extend' as const, version, rows: this.order() }
      : { kind: 'toggle' as const, version };
    this.selection.update((selection) => selectionAfter(selection, action));
  }

  protected tickSeason(season: Season, event: Event): void {
    event.preventDefault();
    const versions = season.rows.map((row) => row.version);
    this.selection.update((selection) => selectionAfter(selection, { kind: 'season', versions }));
  }

  protected tickFromMatrix(version: string): void {
    this.selection.update((selection) => selectionAfter(selection, { kind: 'toggle', version }));
  }

  protected clear(): void {
    this.selection.update((selection) => selectionAfter(selection, { kind: 'clear' }));
  }

  protected fold(season: Season): void {
    this.folds.update((folds) => new Map(folds).set(season.key, !season.isOpen));
  }

  protected launchSelection(): void {
    // In the order of the report, newest first, whatever the order of the ticks.
    const selected = this.selection().selected;
    const versions = (this.patches.value()?.patches ?? [])
      .map((row) => row.version)
      .filter((version) => selected.has(version));
    this.launch.emit({
      ...entryPreset('complete-epoch', this.shownEpoch()),
      origin: 'coverage',
      versions,
    });
  }

  protected completeGaps(): void {
    this.launch.emit(entryPreset('complete-epoch', this.shownEpoch()));
  }

  protected seasonLabel(season: { major: number; year: number | null }): string {
    const { major, year } = season;
    return year === null
      ? this.texts.text('prerender.common.seasons.label_bare', { major })
      : this.texts.text('prerender.common.seasons.label', { major, year });
  }

  protected barOf(season: SeasonShares): BarSegment[] {
    const segment = (key: string, value: number, color: string): BarSegment => ({
      key,
      value,
      color,
      label: this.texts.text(`prerender.common.status.${key}`),
    });
    return [
      segment('complete', season.complete, GOOD),
      segment('partial', season.partial, PARTIAL),
      segment('failed', season.failed, BAD),
      segment('absent', season.absent, TRACK),
    ];
  }

  protected stripLabel(row: PrerenderPatchRow): string {
    return this.texts.text('prerender.common.strip', {
      complete: row.completeLocales,
      total: row.cells.length,
    });
  }
}
