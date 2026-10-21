import { BreakpointObserver } from '@angular/cdk/layout';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import type { PrerenderPassProgress } from '../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../ui/controls/button';
import { duration } from '../../../../format/duration';
import { stamp } from '../../../../format/stamp';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge } from '../../../../widgets/badge';
import type { LaunchPreset } from '../common/scope/launch-preset';
import { PRERENDER_CADENCE } from '../common/scope/prerender-cadence';
import { scopeSummary } from '../common/scope/scope-summary';
import { injectAdminDialogs } from '../dialogs/inject-admin-dialogs';
import { LiveBadge } from '../live/badge/live-badge';
import { injectNow } from '../live/badge/inject-now';
import { UnitMatrix } from '../matrix/grid/unit-matrix';
import { VersionUnitsDialog } from '../matrix/units/version-units-dialog';
import { prerenderTone } from '../prerender-tone';
import { ActivityFeed } from './activity/activity-feed';
import { MonitorStatus } from './alarm/monitor-status';
import { FailuresList } from './activity/failures-list';
import { type ConsequenceKind, consequencesOf } from './actions/consequences-of';
import { injectPassActions } from './actions/inject-pass-actions';
import { PassActionsBar } from './actions/pass-actions-bar';
import { injectPassProgress } from './inject-pass-progress';
import { type MonitorState, mergeProgress } from './merge-progress';
import { FailingBand } from './now/failing-band';
import { failingUnitOf } from './now/failing-unit-of';
import { NowBlock } from './now/now-block';
import { stallOf } from './now/stall-of';
import { ProgressBlock } from './progress/progress-block';
import { serverNow } from './progress/server-now';

/** A part of the monitor folded on a phone. */
type Fold = 'activity' | 'failures' | 'matrix';

const LIVE = 'prerender.live';
const PAUSED = 'paused';
const ENDED = ['completed', 'cancelled'];
const NOT_FOUND = 404;
const SECOND_MS = 1_000;
// Below the `sm` breakpoint the activity, the failures and the matrix fold away.
const PHONE = '(max-width: 639.98px)';

// What the monitor shows of a pass before its first progress: the report's figures alone.
function placeholderOf(pass: PrerenderPassView, generatedAt: string): PrerenderPassProgress {
  return {
    generatedAt,
    pass,
    locales: [],
    isComplete: true,
    versions: [],
    running: [],
    next: [],
    recent: [],
    failures: [],
    unitsWithFailures: 0,
    pulse: { isWorkerEnabled: true, waiting: [] },
  };
}

/**
 * The live monitor of the active pass (§ 4): its state, how fresh the reads are, who opened
 * it and over what; a band when it paused itself, stalls or its unit fails in numbers; its
 * progress, pace and end; the unit running and what comes next; the activity, the failures and the matrix of its units;
 * pause, resume and cancel, each saying its consequences first. It reads the pass's progress
 * itself, every 5 seconds while it runs; once the pass ends it says so (`ended`). It never
 * opens the launch: a relaunch leaves as a preset (`launch`).
 */
@Component({
  selector: 'lodb-live-monitor',
  imports: [
    ActivityFeed,
    AdminCard,
    AdminTextPipe,
    Badge,
    Button,
    FailingBand,
    FailuresList,
    LiveBadge,
    MonitorStatus,
    NowBlock,
    PassActionsBar,
    ProgressBlock,
    UnitMatrix,
  ],
  templateUrl: './live-monitor.html',
  styleUrl: './live-monitor.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LiveMonitor {
  readonly pass = input.required<PrerenderPassView>(); // report.current
  readonly report = input.required<PrerenderReport>();
  readonly launch = output<LaunchPreset>(); // « Relancer N unités »
  readonly changed = output<void>(); // after an action: reread the report
  // The name the contract between the lots fixes (spec § 7.1), though a media element has
  // an `ended` event too: the monitor is no media element.
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly ended = output<number>(); // the pass id seen completed/cancelled

  private readonly texts = injectAdminText();
  private readonly dialogs = injectAdminDialogs();
  private readonly cadence = inject(PRERENDER_CADENCE);
  private readonly now = injectNow(SECOND_MS);
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly folds = signal<ReadonlySet<Fold>>(new Set());
  private endedId: number | null = null;

  protected readonly monitor = injectPassProgress(
    () => this.pass().id,
    () => this.current().state,
  );
  protected readonly actions = injectPassActions(() => this.afterAction());
  protected readonly isPhone = toSignal(
    this.breakpoints.observe(PHONE).pipe(map((state) => state.matches)),
    { initialValue: this.breakpoints.isMatched(PHONE) },
  );
  /** The progress of this pass, once read; undefined before, or while another one shows. */
  protected readonly state = computed(() => {
    const state = this.monitor.value();
    return state?.progress.pass.id === this.pass().id ? state : undefined;
  });
  protected readonly current = computed(() => this.state()?.progress.pass ?? this.pass());
  protected readonly shown = computed<MonitorState>(
    () =>
      this.state() ??
      mergeProgress(undefined, placeholderOf(this.pass(), this.report().generatedAt), 0),
  );
  protected readonly isPaused = computed(() => this.current().state === PAUSED);
  protected readonly every = computed(() =>
    this.isPaused() ? this.cadence.progressPausedMs : this.cadence.progressRunningMs,
  );
  protected readonly serverNow = computed(() => {
    const state = this.state();
    const now = this.now();
    return state ? serverNow(state.progress.generatedAt, state.receivedAt, now) : now;
  });
  protected readonly title = computed(() => {
    const { id, epoch } = this.current();
    return this.texts.text(`${LIVE}.title`, { id, epoch });
  });
  protected readonly tone = computed(() => prerenderTone(this.current().state));
  protected readonly origin = computed(() => {
    const { authorName, createdAt } = this.current();
    const date = stamp(createdAt, 'minute');
    return authorName
      ? this.texts.text(`${LIVE}.origin`, { date, author: authorName })
      : this.texts.text(`${LIVE}.origin_auto`, { date });
  });
  protected readonly scope = computed(() => scopeSummary(this.current().scope, this.texts));
  protected readonly pausedSince = computed(() => {
    const { pausedAt } = this.current();
    if (!this.isPaused() || !pausedAt) {
      return null;
    }
    const length = duration((this.serverNow() - Date.parse(pausedAt)) / SECOND_MS);
    return this.texts.text(`${LIVE}.paused_since`, { duration: length });
  });
  protected readonly stall = computed(() => {
    const state = this.state();
    return state ? stallOf(state.progress, this.serverNow(), state.written.at) : null;
  });
  protected readonly failing = computed(() => {
    const state = this.state();
    return state ? failingUnitOf(state.progress) : null;
  });
  protected readonly stallTitle = computed(() => {
    const stall = this.stall();
    return stall?.kind === 'stalled'
      ? this.texts.text(`${LIVE}.stall.title`, { duration: duration(stall.idleSeconds) })
      : '';
  });
  protected readonly running = computed(() =>
    (this.state()?.progress.running ?? []).map((unit) => ({
      version: unit.version,
      locale: unit.locale,
      share: unit.pagesTotal > 0 ? unit.cursor / unit.pagesTotal : 0,
    })),
  );
  protected readonly details = computed(() => {
    const progress = this.state()?.progress;
    return progress ? [...progress.running, ...progress.recent, ...progress.failures] : [];
  });

  constructor() {
    effect(() => {
      const pass = this.state()?.progress.pass;
      if (pass && ENDED.includes(pass.state) && this.endedId !== pass.id) {
        this.endedId = pass.id;
        untracked(() => this.ended.emit(pass.id));
      }
    });
    // A pass the API no longer knows: the report says what is active now.
    effect(() => {
      if (this.monitor.failure()?.status === NOT_FOUND) {
        untracked(() => this.changed.emit());
      }
    });
  }

  protected isOpen(fold: Fold): boolean {
    return !this.isPhone() || this.folds().has(fold);
  }

  protected flip(fold: Fold): void {
    this.folds.update((folds) => {
      const next = new Set(folds);
      return next.delete(fold) ? next : next.add(fold);
    });
  }

  protected openVersion(version: string): void {
    const pass = this.current();
    this.dialogs.open(VersionUnitsDialog, {
      labelledBy: VersionUnitsDialog.HEADING_ID,
      data: { passId: pass.id, version, isActive: !ENDED.includes(pass.state) },
      size: 'wide',
    });
  }

  protected async pause(): Promise<void> {
    if (await this.ask('pause')) {
      await this.actions.pause(this.current().id);
    }
  }

  protected async resume(): Promise<void> {
    // A pause of an administrator resumes at once; one of its own asks first.
    if (!this.current().pauseReason || (await this.ask('resume'))) {
      await this.actions.resume(this.current().id);
    }
  }

  protected async cancel(): Promise<void> {
    if (await this.ask('cancel')) {
      await this.actions.cancel(this.current().id);
    }
  }

  private ask(kind: ConsequenceKind): Promise<boolean> {
    const context = {
      pass: this.current(),
      running: this.state()?.progress.running ?? [],
      report: this.report(),
    };
    return this.dialogs.confirm(consequencesOf(kind, context, this.texts));
  }

  private afterAction(): void {
    this.monitor.reload();
    this.changed.emit();
  }
}
