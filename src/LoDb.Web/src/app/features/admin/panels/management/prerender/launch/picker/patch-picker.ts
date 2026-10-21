import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { Button } from '../../../../../../../ui/controls/button';
import { Field } from '../../../../../../../ui/controls/field';
import { FigurePipe } from '../../../../../format/figure-pipe';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../../widgets/badge';
import { CellStrip } from '../../bars/cell-strip';
import { type PatchStatus, patchStatusOf } from '../../common/patches/patch-status-of';
import { seasonOf } from '../../common/patches/season-of';
import type { PatchSelection, SelectionAction } from '../../common/selection/patch-selection';
import { injectAnnouncement } from '../dialog/inject-announcement';
import { PickerFocus } from './picker-focus';
import { type PickerCommand, pickerActionOf } from './picker-key';
import { type PatchLine, type SeasonLine, type StatusFilter, pickerRows } from './picker-rows';

/** A tool of the list, each a key of `launcher.patches`. */
type PickerTool = 'all' | 'none_selected' | 'invert' | 'uncovered' | 'partials';

const STATUS_FILTERS: readonly StatusFilter[] = ['all', 'complete', 'partial', 'absent', 'failed'];
const TOOLS: readonly PickerTool[] = ['all', 'none_selected', 'invert', 'uncovered', 'partials'];
const PATCHES = 'prerender.launcher.patches';
const SEASONS = 'prerender.common.seasons';
// PassRequestReader.MaxPriorityVersions: past it, ☆ is disabled.
const MAX_PRIORITY = 100;
// The code of a season's cell held by each of its patches (pickerRows).
const COMPLETE = 'c';
const STATUS_TONES: Readonly<Record<PatchStatus, Tone>> = {
  complete: 'good',
  complete_with_failures: 'warn',
  partial: 'warn',
  absent: 'muted',
  failed: 'bad',
};

/**
 * Step 1 of the launch (§ 3.3): the older patches by season, as a treegrid held in row mode
 * (§ 3.3.4), with a text and a status filter, a range by its bounds, the tools, and the whole
 * keyboard of § 3.3.5. It holds no selection of its own: each gesture becomes an action of
 * `selectionAfter`, sent to the dialog, which hands the selection back.
 */
@Component({
  selector: 'lodb-patch-picker',
  imports: [AdminTextPipe, Badge, Button, CellStrip, Field, FigurePipe],
  templateUrl: './patch-picker.html',
  styleUrl: './patch-picker.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PatchPicker {
  /** The coverage of the epoch written. */
  readonly patches = input.required<PrerenderPatchesReport>();
  readonly selection = input.required<PatchSelection>();
  /** Whether a grouped selection can be undone. */
  readonly canUndo = input(false);
  readonly acted = output<SelectionAction>();
  readonly undone = output<void>();

  private readonly texts = injectAdminText();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  protected readonly statuses = STATUS_FILTERS;
  protected readonly tools = TOOLS;
  protected readonly query = signal('');
  protected readonly status = signal<StatusFilter>('all');
  protected readonly isToolsOpen = signal(false);
  protected readonly focus = new PickerFocus();
  protected readonly versions = computed(() => this.patches().patches.map((row) => row.version));
  protected readonly rangeFrom = linkedSignal(() => this.versions()[0] ?? '');
  protected readonly rangeTo = linkedSignal(() => this.versions().at(-1) ?? '');
  // Read once: the newest season, and each season the preset ticked a patch of (§ 3.3.1).
  private readonly open = linkedSignal<ReadonlySet<string>>(() =>
    untracked(() => this.openAtFirst()),
  );
  protected readonly rows = computed(() =>
    pickerRows(
      this.patches().patches,
      { query: this.query(), status: this.status(), open: this.open() },
      this.selection(),
    ),
  );
  protected readonly isPriorityFull = computed(
    () => this.selection().priority.length >= MAX_PRIORITY,
  );
  protected readonly tabStop = computed(() => this.focus.keyIn(this.rows().lines));
  protected readonly count = computed(() => this.countLine());
  protected readonly announced = injectAnnouncement(() =>
    this.texts.text(`${PATCHES}.announce`, { count: this.selection().selected.size }),
  );

  /** « 16.x · 2026 », or « 0.x » for a season outside the yearly ones. */
  protected seasonLabel(line: SeasonLine): string {
    const { major, year } = line.season;
    return year === null
      ? this.texts.text(`${SEASONS}.label_bare`, { major: line.season.key })
      : this.texts.text(`${SEASONS}.label`, { major, year });
  }

  /** The locales a season holds on each of its patches. */
  protected completeIn(cells: string): number {
    return [...cells].filter((cell) => cell === COMPLETE).length;
  }

  protected toneOf(status: PatchStatus): Tone {
    return STATUS_TONES[status];
  }

  protected onKey(event: KeyboardEvent, line: PatchLine | SeasonLine): void {
    const command = pickerActionOf(event, line, this.rows());
    if (command) {
      event.preventDefault();
      this.apply(command);
    }
  }

  protected onRowClick(event: MouseEvent, line: PatchLine): void {
    this.focus.moveTo(line.key);
    this.acted.emit(
      event.shiftKey
        ? { kind: 'extend', version: line.key, rows: this.rows().order }
        : { kind: 'toggle', version: line.key },
    );
  }

  /** A Shift click draws a range: it must not select the text of the rows. */
  protected holdText(event: MouseEvent): void {
    if (event.shiftKey) {
      event.preventDefault();
    }
  }

  protected toggleFold(line: SeasonLine): void {
    this.focus.moveTo(line.key);
    this.fold(line.season.key, !line.isOpen);
  }

  protected tickSeason(event: Event, line: SeasonLine): void {
    event.stopPropagation();
    this.acted.emit({ kind: 'season', versions: line.versions });
  }

  protected prioritise(event: Event, line: PatchLine): void {
    event.stopPropagation();
    this.acted.emit({ kind: 'priority', version: line.key });
  }

  protected useTool(tool: PickerTool): void {
    const { order } = this.rows();
    const visible = new Set(order);
    // The visible patches of these statuses, those of a folded season included.
    const shown = (statuses: readonly PatchStatus[]) =>
      this.patches()
        .patches.filter((row) => visible.has(row.version))
        .filter((row) => statuses.includes(patchStatusOf(row)))
        .map((row) => row.version);
    const actions: Readonly<Record<PickerTool, () => SelectionAction>> = {
      all: () => ({ kind: 'set', versions: order, on: true }),
      none_selected: () => ({ kind: 'clear' }),
      invert: () => ({ kind: 'invert', rows: order }),
      uncovered: () => ({ kind: 'replace', versions: shown(['absent', 'failed']) }),
      partials: () => ({ kind: 'replace', versions: shown(['partial', 'complete_with_failures']) }),
    };
    this.acted.emit(actions[tool]());
  }

  /** « Sélectionner la plage »: every version between the bounds, whatever the filters. */
  protected applyRange(): void {
    const versions = this.versions();
    const from = versions.indexOf(this.rangeFrom());
    const to = versions.indexOf(this.rangeTo());
    if (from >= 0 && to >= 0) {
      const range = versions.slice(Math.min(from, to), Math.max(from, to) + 1);
      this.acted.emit({ kind: 'set', versions: range, on: true });
    }
  }

  protected filterBy(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  /** Escape clears the filter before it closes the dialog; ↓ goes down to the list. */
  protected onFilterKey(event: KeyboardEvent): void {
    if (event.key === 'Escape' && this.query() !== '') {
      event.preventDefault();
      event.stopPropagation();
      this.query.set('');
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      const first = this.rows().lines[0];
      if (first) {
        this.moveFocus(first.key);
      }
    }
  }

  protected chooseStatus(event: Event): void {
    this.status.set((event.target as HTMLSelectElement).value as StatusFilter);
  }

  protected readFrom(event: Event, bound: 'from' | 'to'): void {
    const version = (event.target as HTMLSelectElement).value;
    (bound === 'from' ? this.rangeFrom : this.rangeTo).set(version);
  }

  private apply(command: PickerCommand): void {
    if (command.action) {
      this.acted.emit(command.action);
    }
    if (command.fold) {
      this.fold(command.fold.season, command.fold.isOpen);
    }
    if (command.undo) {
      this.undone.emit();
    }
    if (command.filter) {
      this.host.querySelector<HTMLInputElement>('[data-filter]')?.focus();
    }
    const lines = this.rows().lines;
    const to = command.search
      ? this.focus.search(command.search, lines, Date.now())
      : command.focus;
    if (to) {
      this.moveFocus(to);
    }
  }

  private fold(season: string, isOpen: boolean): void {
    const open = new Set(this.open());
    if (isOpen) {
      open.add(season);
    } else {
      open.delete(season);
    }
    this.open.set(open);
  }

  // The DOM focus follows once the row is rendered: unfolding may have just added it.
  private moveFocus(key: string): void {
    this.focus.moveTo(key);
    afterNextRender(
      () => this.host.querySelector<HTMLElement>(`[data-key="${CSS.escape(key)}"]`)?.focus(),
      { injector: this.injector },
    );
  }

  private openAtFirst(): ReadonlySet<string> {
    const { selected } = this.selection();
    const rows = this.patches().patches;
    const ticked = rows.filter((row) => selected.has(row.version));
    return new Set([...rows.slice(0, 1), ...ticked].map((row) => seasonOf(row.version).key));
  }

  private countLine(): string {
    const { selected, priority } = this.selection();
    const shown = new Set(this.rows().order);
    const hidden = [...selected].filter((version) => !shown.has(version)).length;
    return [
      this.texts.text(`${PATCHES}.count`, { selected: selected.size }),
      hidden > 0 ? this.texts.text(`${PATCHES}.count_hidden`, { hidden }) : '',
      priority.length > 0
        ? this.texts.text(`${PATCHES}.count_priority`, { count: priority.length })
        : '',
    ]
      .filter((part) => part !== '')
      .join(' · ');
  }
}
