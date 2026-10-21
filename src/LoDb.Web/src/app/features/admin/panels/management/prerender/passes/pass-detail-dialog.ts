import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  inject,
  signal,
} from '@angular/core';
import { type Observable, map, of } from 'rxjs';
import { API_BASE_URL } from '../../../../../../core/api/api-base-url';
import { readAdminPrerenderPassProgress } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-pass-progress';
import { readAdminPrerenderPatches } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-patches';
import type { PrerenderPassProgress } from '../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../ui/controls/button';
import { DialogFrame } from '../../../../../../ui/overlays/dialog-frame';
import { duration } from '../../../../format/duration';
import { figure } from '../../../../format/figure';
import { stamp } from '../../../../format/stamp';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge } from '../../../../widgets/badge';
import { failuresPreset } from '../common/scope/failures-preset';
import type { LaunchPreset } from '../common/scope/launch-preset';
import { scopeSummary } from '../common/scope/scope-summary';
import { injectAdminDialogs } from '../dialogs/inject-admin-dialogs';
import { decimalBytes } from '../format/decimal-bytes';
import { injectLivePoll } from '../live/poll/inject-live-poll';
import { UnitMatrix } from '../matrix/grid/unit-matrix';
import { VersionUnitsDialog } from '../matrix/units/version-units-dialog';
import { FailuresList } from '../monitor/activity/failures-list';
import { launchLockOf } from '../overview/state/launch-lock-of';
import { prerenderTone } from '../prerender-tone';
import { relaunchPreset } from './relaunch-preset';

/** The pass whose detail the dialog shows, and the report it was opened from. */
export interface PassDetailData {
  readonly pass: PrerenderPassView;
  readonly report: PrerenderReport;
}

/** What the dialog closes on: a relaunch to open, the way to the monitor, or nothing. */
export type PassDetailResult = LaunchPreset | 'follow' | undefined;

const DETAIL = 'prerender.passes.detail';
const LIVE = 'prerender.live';
const RETRY = 'prerender.common.retry';
const ENDED = ['completed', 'cancelled'];

/**
 * The detail of a pass of the history (§ 5.4): its state, who opened it and when it ended;
 * its perimeter, the list of its versions on demand; its outcome in units, pages, volume,
 * running time and pace; the matrix of its units and its failures, from one complete read
 * of its progress. It offers « Relancer N unités (≈ P pages) » and « Relancer ce périmètre »,
 * both waiting while a pass is active, or « Suivre en direct » for the active pass.
 */
@Component({
  selector: 'lodb-pass-detail-dialog',
  imports: [AdminTextPipe, Badge, Button, DialogFrame, FailuresList, UnitMatrix],
  templateUrl: './pass-detail-dialog.html',
  // The relaunch of the failures is the dialog's own, in its foot: the list's is hidden. Not
  // encapsulated so that the rule reaches inside the list, scoped by the dialog's tag.
  styles: `
    lodb-pass-detail-dialog lodb-failures-list :is([data-relaunch], #prerender-failures-later) {
      display: none;
    }
  `,
  encapsulation: ViewEncapsulation.None,
  host: { class: 'admin-overlay block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PassDetailDialog {
  static readonly HEADING_ID = 'prerender-pass-detail-title';

  protected readonly ref = inject<DialogRef<PassDetailResult>>(DialogRef);
  protected readonly data = inject<PassDetailData>(DIALOG_DATA);
  protected readonly headingId = PassDetailDialog.HEADING_ID;

  private readonly texts = injectAdminText();
  private readonly http = inject(HttpClient);
  private readonly rootUrl = inject(API_BASE_URL);
  private readonly dialogs = injectAdminDialogs();

  protected readonly pass = this.data.pass;
  protected readonly isActive = this.data.report.current?.id === this.pass.id;
  protected readonly lock = launchLockOf(this.data.report);
  protected readonly isListOpen = signal(false);
  // One complete read: the matrix, the failures and the list of the perimeter.
  protected readonly progress = injectLivePoll<PrerenderPassProgress>({
    load: () =>
      readAdminPrerenderPassProgress(this.http, this.rootUrl, { id: this.pass.id }).pipe(
        map((response) => response.body),
      ),
    every: () => null,
  });
  // A range from before the lists names no version: the coverage's older patches do.
  private readonly patches = injectLivePoll<readonly string[] | null>({
    load: () => this.loadPatches(),
    every: () => null,
  });

  protected readonly title = this.texts.text(`${DETAIL}.title`, {
    id: this.pass.id,
    epoch: this.pass.epoch,
  });
  protected readonly tone = prerenderTone(this.pass.state);
  protected readonly head = computed(() => {
    const { authorName: author, createdAt, finishedAt } = this.pass;
    const date = stamp(createdAt, 'minute');
    const origin = author
      ? this.texts.text(`${LIVE}.origin`, { date, author })
      : this.texts.text(`${LIVE}.origin_auto`, { date });
    const ended = finishedAt
      ? this.texts.text(`${DETAIL}.ended`, { date: stamp(finishedAt, 'minute') })
      : null;
    return ended ? `${origin} · ${ended}` : origin;
  });
  protected readonly scope = computed(() => scopeSummary(this.pass.scope, this.texts));
  protected readonly scopeVersions = computed(() => this.progress.value()?.scopeVersions ?? []);
  protected readonly legacyRange = computed(() => {
    const { oldest, newest } = this.pass.scope;
    const patches = this.patches.value();
    if (!(oldest || newest) || !patches) {
      return null;
    }
    const count = this.relaunch()?.versions;
    return this.texts.text(`${DETAIL}.legacy_range`, {
      oldest: oldest ?? '—',
      newest: newest ?? '—',
      count: Array.isArray(count) ? count.length : 0,
    });
  });
  protected readonly outcome = computed(() => this.outcomeOf(this.pass));
  protected readonly failures = computed(() => {
    const progress = this.progress.value();
    return progress ? failuresPreset(this.pass, progress.versions) : null;
  });
  protected readonly failuresLabel = computed(() => {
    const failures = this.failures();
    if (!failures) {
      return '';
    }
    const key = failures.hasGaps ? 'units_gaps' : 'units';
    return this.texts.text(`${RETRY}.${key}`, { units: failures.units, pages: failures.pages });
  });
  protected readonly relaunch = computed(() => {
    const progress = this.progress.value();
    if (!progress || this.patches.value() === undefined) {
      return null;
    }
    return relaunchPreset(this.pass, {
      scopeVersions: progress.scopeVersions ?? null,
      patches: this.patches.value() ?? [],
      servedEpoch: this.data.report.servedEpoch,
    });
  });
  protected readonly lockReason = computed(() => {
    const lock = this.lock;
    if (!lock) {
      return '';
    }
    return lock.kind === 'active'
      ? this.texts.text(`${DETAIL}.locked`)
      : this.texts.text(`prerender.overview.cta.locked_${lock.kind}`, lock);
  });
  protected readonly details = computed(() => {
    const progress = this.progress.value();
    return progress ? [...progress.running, ...progress.recent, ...progress.failures] : [];
  });

  protected openVersion(version: string): void {
    this.dialogs.open(VersionUnitsDialog, {
      labelledBy: VersionUnitsDialog.HEADING_ID,
      data: { passId: this.pass.id, version, isActive: !ENDED.includes(this.pass.state) },
      size: 'wide',
    });
  }

  protected relaunchWith(preset: LaunchPreset | null | undefined): void {
    if (preset && !this.lock) {
      this.ref.close(preset);
    }
  }

  protected reason(): string {
    const failure = this.progress.failure();
    return failure
      ? this.texts.text(`state.reasons.${failure.kind}`, { status: failure.status })
      : '';
  }

  private outcomeOf(pass: PrerenderPassView): readonly string[] {
    const { units } = pass;
    const pace = pass.pagesPerMinute;
    return [
      this.texts.text(`${DETAIL}.units`, { ...units, pending: units.pending + units.running }),
      this.texts.text(`${DETAIL}.pages`, {
        written: pass.pagesWritten,
        skipped: figure(pass.pagesSkipped),
        failed: figure(pass.pagesFailed),
      }),
      decimalBytes(pass.bytesWritten),
      `${this.texts.text(`${LIVE}.active.label`)} : ${duration(pass.activeSeconds)}`,
      ...(pace === null || pace === undefined
        ? []
        : [this.texts.text(`${DETAIL}.pace`, { pace: Math.round(pace) })]),
    ];
  }

  private loadPatches(): Observable<readonly string[] | null> {
    const { oldest, newest } = this.pass.scope;
    if (!(oldest || newest)) {
      return of(null);
    }
    return readAdminPrerenderPatches(this.http, this.rootUrl, {}).pipe(
      map((response) => response.body.patches.map((row) => row.version)),
    );
  }
}
