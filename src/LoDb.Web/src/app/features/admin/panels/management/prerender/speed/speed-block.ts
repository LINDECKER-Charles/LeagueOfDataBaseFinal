import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { setAdminPrerenderSpeed } from '../../../../../../core/api/generated/fn/admin-prerender/set-admin-prerender-speed';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import type { PrerenderSpeedReport } from '../../../../../../core/api/generated/models/prerender-speed-report';
import { Button } from '../../../../../../ui/controls/button';
import { Field } from '../../../../../../ui/controls/field';
import { stamp } from '../../../../format/stamp';
import type { AdminNotice } from '../../../../layout/admin-notices';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { AdminCommand } from '../../../../shared/http/admin-command';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { speedFigures } from './math/speed-figures';
import { speedLines } from './speed-lines';
import { SpeedReportStore } from './speed-report-store';

/** The mode the operator picks. */
export type SpeedMode = 'adaptive' | 'manual';

const MANUAL: SpeedMode = 'manual';
// The slider moves by half a minute's floor: fine enough, and few stops on a phone.
const STEP = 30;

/**
 * « Vitesse du précalcul » (§ « Vitesse réglable »): adaptive or manual, the pages a minute of
 * the manual mode and its emergency brake, applied live; beside it, the VPS, the renderers,
 * the visitors and the API as last read, and what the pace looked at would take, leave and
 * cost, and when the active pass and the catalogue would end at it.
 */
@Component({
  selector: 'lodb-speed-block',
  imports: [AdminCard, AdminTextPipe, Button, Field],
  templateUrl: './speed-block.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpeedBlock {
  /** The pre-rendering's report: the active pass, the served epoch, the prices. */
  readonly report = input.required<PrerenderReport>();

  private readonly texts = injectAdminText();
  private readonly command = inject(AdminCommand);

  protected readonly step = STEP;
  protected readonly speed = inject(SpeedReportStore).report;
  protected readonly busy = signal(false);
  protected readonly outcome = signal<AdminNotice | null>(null);
  /** The mode and the pace shown, the stored ones until the operator changes them. */
  protected readonly mode = linkedSignal<SpeedMode>(() => this.storedMode());
  protected readonly pace = linkedSignal<number>(
    () => this.speed.value()?.setting.rendersPerMinute ?? 0,
  );
  protected readonly isManual = computed(() => this.mode() === MANUAL);
  /** Whether the manual target is past what the renderers can sustain (never at the threshold). */
  protected readonly isOverWarning = computed(() => {
    const limits = this.speed.value()?.limits;
    return !!limits && this.pace() > limits.manualWarnRendersPerMinute;
  });
  protected readonly isChanged = computed(() => {
    const setting = this.speed.value()?.setting;
    return !!setting && (setting.mode !== this.mode() || setting.rendersPerMinute !== this.pace());
  });
  protected readonly lines = computed(() => {
    const speed = this.speed.value();
    return speed ? speedLines(speed, this.figuresOf(speed), this.texts) : null;
  });
  protected readonly applied = computed(() => {
    const at = this.speed.value()?.setting.updatedAt;
    return at ? stamp(at, 'minute') : null;
  });

  protected choose(mode: SpeedMode): void {
    this.mode.set(mode);
  }

  protected typed(event: Event): void {
    const speed = this.speed.value();
    const number = Math.round(Number((event.target as HTMLInputElement).value));
    if (speed && Number.isFinite(number)) {
      this.pace.set(Math.min(speed.limits.manualMaxRendersPerMinute, Math.max(1, number)));
    }
  }

  protected async apply(): Promise<void> {
    this.busy.set(true);
    try {
      const body = { mode: this.mode(), rendersPerMinute: this.pace() };
      const done = { key: 'prerender.done.speed' };
      this.outcome.set((await this.command.attempt(setAdminPrerenderSpeed, { body }, done)).notice);
    } finally {
      this.busy.set(false);
      this.speed.reload();
    }
  }

  private storedMode(): SpeedMode {
    return this.speed.value()?.setting.mode === MANUAL ? MANUAL : 'adaptive';
  }

  // Adaptive, the forecast is of the ceiling, the most the regulator would reach.
  private figuresOf(speed: PrerenderSpeedReport) {
    const pace = this.isManual() ? this.pace() : speed.limits.maxRendersPerMinute;
    return speedFigures({ pace, speed, report: this.report() });
  }
}
