import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../../core/api/api-base-url';
import { readAdminPrerenderPassProgress } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-pass-progress';
import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { Button } from '../../../../../../../ui/controls/button';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import { AdminCard } from '../../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { type BarSegment, StackedBar } from '../../bars/stacked-bar';
import { failuresPreset } from '../../common/scope/failures-preset';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { retryLabel } from '../../common/scope/retry-label';
import { decimalBytes } from '../../format/decimal-bytes';
import { injectLivePoll } from '../../live/poll/inject-live-poll';
import { progressSegments } from '../progress/progress-segments';

const RECAP = 'prerender.live.recap';
const COMPLETED = 'completed';
// The pass this visitor dismissed, kept in this browser only; the page reads it too.
const DISMISSED_KEY = 'lodb.admin.prerender.recap-dismissed';

/**
 * The end of a pass (§ 4.8), in the place of the monitor: completed or cancelled, its units,
 * pages written and failed, running time and volume, its final bar. It reads its whole
 * progress once to offer, completed, « Relancer 17 unités (≈ 19 000 pages) » when something is
 * to render again; cancelled, « Finir cette passe (4 unités, ≈ 4 316 pages) », what it left,
 * before « Compléter l'epoch 2 », which takes every gap of the epoch. « Voir le détail » opens it in the
 * history; « Masquer » remembers the pass in this browser and gives the place back.
 */
@Component({
  selector: 'lodb-pass-recap',
  imports: [AdminCard, AdminTextPipe, Button, StackedBar],
  templateUrl: './pass-recap.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PassRecap {
  readonly pass = input.required<PrerenderPassView>();
  /** The epoch nginx serves: a relaunch of a retired epoch goes to it. */
  readonly servedEpoch = input.required<number>();
  readonly launch = output<LaunchPreset>();
  readonly detail = output<number>();
  readonly dismissed = output<number>();

  private readonly texts = injectAdminText();
  private readonly http = inject(HttpClient);
  private readonly rootUrl = inject(API_BASE_URL);
  private readonly storage = inject(DOCUMENT).defaultView?.localStorage;

  // Read once, whole: what the pass left undone, failed or never reached.
  private readonly progress = injectLivePoll<PrerenderPassProgress>({
    load: () =>
      readAdminPrerenderPassProgress(this.http, this.rootUrl, { id: this.pass().id }).pipe(
        map((response) => response.body),
      ),
    every: () => null,
  });

  protected readonly isCompleted = computed(() => this.pass().state === COMPLETED);
  protected readonly title = computed(() =>
    this.texts.text(`${RECAP}.${this.isCompleted() ? 'completed' : 'cancelled'}`, {
      id: this.pass().id,
    }),
  );
  protected readonly summary = computed(() => {
    const pass = this.pass();
    return this.texts.text(`${RECAP}.summary`, {
      units: pass.units.total,
      written: pass.pagesWritten,
      failed: figure(pass.pagesFailed),
      duration: duration(pass.activeSeconds),
      size: decimalBytes(pass.bytesWritten),
    });
  });
  protected readonly segments = computed<BarSegment[]>(() =>
    progressSegments(this.pass()).parts.map((part) => ({
      ...part,
      // Counted in units before any listed its pages: « terminée » has no segment key.
      label: this.texts.text(
        part.key === COMPLETED
          ? 'prerender.states.completed'
          : `prerender.live.segments.${part.key}`,
      ),
    })),
  );
  protected readonly relaunch = computed(() => {
    const progress = this.progress.value();
    return progress ? failuresPreset(this.pass(), progress.versions, this.servedEpoch()) : null;
  });
  // Cancelled, the pass is finished first: what it left, then its epoch's other gaps.
  protected readonly relaunchLabel = computed(() => {
    const relaunch = this.relaunch();
    if (!relaunch) {
      return '';
    }
    return this.isCompleted()
      ? retryLabel(relaunch, this.texts)
      : this.texts.text(`${RECAP}.finish`, { units: relaunch.units, pages: relaunch.pages });
  });

  protected complete(): void {
    this.launch.emit({
      origin: 'complete-epoch',
      versions: 'incomplete',
      priority: [],
      locales: null,
      pageTypes: null,
      epoch: this.pass().epoch,
      skipComplete: true,
      fromPass: null,
    });
  }

  protected dismiss(): void {
    const id = this.pass().id;
    try {
      this.storage?.setItem(DISMISSED_KEY, String(id));
    } catch {
      // Private browsing or storage refused: dismissed for this page only.
    }
    this.dismissed.emit(id);
  }
}
