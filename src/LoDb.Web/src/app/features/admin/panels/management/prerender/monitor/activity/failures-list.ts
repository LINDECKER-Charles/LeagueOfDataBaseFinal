import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import { Button } from '../../../../../../../ui/controls/button';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../../widgets/badge';
import { failuresPreset } from '../../common/scope/failures-preset';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { prerenderTone } from '../../prerender-tone';

const FAILURES = 'prerender.live.failures';
const RETRY = 'prerender.common.retry';
const ACTIVE = ['running', 'paused'];
const FAILED = 'failed';
const LIST_ID = 'prerender-failures-list';
const LATER_ID = 'prerender-failures-later';

/**
 * The failures of the pass (§ 4.6): how many units failed or lost pages, then on demand the
 * 20 latest, each with its pages, the reason in the pass's English and its version's detail;
 * past 20, where the others are. « Relancer 17 unités (≈ 19 000 pages) » says what it renders
 * again, a unit with failed pages being rendered whole; it waits for the pass to end, as one
 * pass is active at a time.
 */
@Component({
  selector: 'lodb-failures-list',
  imports: [AdminTextPipe, Badge, Button],
  templateUrl: './failures-list.html',
  host: { class: 'flex min-w-0 flex-col gap-3' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FailuresList {
  readonly progress = input.required<PrerenderPassProgress>();
  /** The epoch nginx serves: a relaunch of a retired epoch goes to it. */
  readonly servedEpoch = input.required<number>();
  /** The relaunch of the failures, for the page to open the launch with. */
  readonly launch = output<LaunchPreset>();
  /** A version's detail, asked for. */
  readonly opened = output<string>();

  private readonly texts = injectAdminText();

  protected readonly listId = LIST_ID;
  protected readonly laterId = LATER_ID;
  protected readonly isOpen = signal(false);
  protected readonly count = computed(() => this.progress().unitsWithFailures);
  protected readonly relaunch = computed(() => {
    const { pass, versions } = this.progress();
    return failuresPreset(pass, versions, this.servedEpoch());
  });
  protected readonly relaunchLabel = computed(() => {
    const relaunch = this.relaunch();
    if (!relaunch) {
      return '';
    }
    const key = relaunch.hasGaps ? 'units_gaps' : 'units';
    return this.texts.text(`${RETRY}.${key}`, { units: relaunch.units, pages: relaunch.pages });
  });
  protected readonly isActive = computed(() => ACTIVE.includes(this.progress().pass.state));
  protected readonly others = computed(() =>
    Math.max(0, this.count() - this.progress().failures.length),
  );

  protected toneOf(unit: PrerenderUnitView): Tone {
    return unit.state === FAILED ? prerenderTone(FAILED) : 'warn';
  }

  protected pagesOf(unit: PrerenderUnitView): string {
    return this.texts.text(`${FAILURES}.pages`, {
      failed: unit.pagesFailed,
      total: figure(unit.pagesTotal),
    });
  }

  protected emitRelaunch(): void {
    const relaunch = this.relaunch();
    if (relaunch && !this.isActive()) {
      this.launch.emit(relaunch.preset);
    }
  }
}
