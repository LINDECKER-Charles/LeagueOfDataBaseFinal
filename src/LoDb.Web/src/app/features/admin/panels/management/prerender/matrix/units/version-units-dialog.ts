import { DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, type Signal, computed, inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../../core/api/api-base-url';
import { readAdminPrerenderPassVersion } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-pass-version';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import type { PrerenderVersionUnits } from '../../../../../../../core/api/generated/models/prerender-version-units';
import { Button } from '../../../../../../../ui/controls/button';
import { DialogFrame } from '../../../../../../../ui/overlays/dialog-frame';
import { figure } from '../../../../../format/figure';
import { FigurePipe } from '../../../../../format/figure-pipe';
import { StampPipe } from '../../../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../../widgets/badge';
import { type BarSegment, StackedBar } from '../../bars/stacked-bar';
import { localeName } from '../../common/patches/locale-name';
import { PRERENDER_CADENCE } from '../../common/scope/prerender-cadence';
import { DecimalBytesPipe } from '../../format/decimal-bytes-pipe';
import { type LivePoll, injectLivePoll } from '../../live/poll/inject-live-poll';
import { prerenderTone } from '../../prerender-tone';

/** The version of a pass whose units the dialog shows. */
export interface VersionUnitsData {
  readonly passId: number;
  readonly version: string;
  readonly isActive: boolean;
}

const RUNNING = 'running';
const SEGMENTS = 'prerender.live.segments';
const COLUMNS = ['locale', 'state', 'progress', 'pages', 'volume', 'started', 'ended', 'failure'];

/**
 * The units of one version of a pass, a row per locale (§ 6.8): state, progress, pages
 * written, skipped and failed, volume, start, last change and the reason of a failure, in
 * the monospace English the pass writes it in. Read on opening, then every 10 seconds while
 * one of its units runs in an active pass. Opened through `injectAdminDialogs().open`.
 */
@Component({
  selector: 'lodb-version-units-dialog',
  imports: [
    AdminTextPipe,
    Badge,
    Button,
    DecimalBytesPipe,
    DialogFrame,
    FigurePipe,
    StackedBar,
    StampPipe,
  ],
  templateUrl: './version-units-dialog.html',
  host: { class: 'admin-overlay block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VersionUnitsDialog {
  static readonly HEADING_ID = 'prerender-version-units-title';

  protected readonly data = inject<VersionUnitsData>(DIALOG_DATA);
  protected readonly headingId = VersionUnitsDialog.HEADING_ID;
  protected readonly columns = COLUMNS;

  private readonly texts = injectAdminText();
  private readonly http = inject(HttpClient);
  private readonly rootUrl = inject(API_BASE_URL);
  private readonly cadence = inject(PRERENDER_CADENCE);

  protected readonly units: LivePoll<PrerenderVersionUnits> = injectLivePoll({
    load: () =>
      readAdminPrerenderPassVersion(this.http, this.rootUrl, {
        id: this.data.passId,
        version: this.data.version,
      }).pipe(map((response) => response.body)),
    every: () => (this.isRunning() ? this.cadence.versionMs : null),
  });
  protected readonly title = computed(() =>
    this.texts.text('prerender.common.units.title', {
      version: this.data.version,
      id: this.data.passId,
    }),
  );
  private readonly isRunning: Signal<boolean> = computed(
    () =>
      this.data.isActive &&
      (this.units.value()?.units.some((unit) => unit.state === RUNNING) ?? false),
  );

  protected languageOf(locale: string): string {
    return localeName(locale);
  }

  protected toneOf(state: string): Tone {
    return prerenderTone(state);
  }

  protected segmentsOf(unit: PrerenderUnitView): BarSegment[] {
    const left = unit.pagesTotal - unit.pagesWritten - unit.pagesSkipped - unit.pagesFailed;
    const segment = (key: string, value: number, color: string): BarSegment => ({
      key,
      value,
      color,
      label: this.texts.text(`${SEGMENTS}.${key}`),
    });
    return [
      segment('written', unit.pagesWritten, 'var(--color-good)'),
      segment(
        'skipped',
        unit.pagesSkipped,
        'color-mix(in srgb, var(--color-gold) 70%, transparent)',
      ),
      segment('failed', unit.pagesFailed, 'var(--color-bad)'),
      segment('left', Math.max(0, left), 'var(--color-track)'),
    ];
  }

  protected progressOf(unit: PrerenderUnitView): string {
    const processed = unit.pagesWritten + unit.pagesSkipped + unit.pagesFailed;
    return `${figure(processed)} / ${figure(unit.pagesTotal)}`;
  }

  protected reason(): string {
    const failure = this.units.failure();
    return failure
      ? this.texts.text(`state.reasons.${failure.kind}`, { status: failure.status })
      : '';
  }
}
