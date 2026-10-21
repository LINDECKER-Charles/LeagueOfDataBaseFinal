import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../../ui/controls/button';
import { figure } from '../../../../../format/figure';
import { stamp } from '../../../../../format/stamp';
import { AdminCard } from '../../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import type { LaunchLock } from '../state/launch-lock-of';
import { entryPreset } from './entry-preset';

const IDLE = 'prerender.overview.idle';

/**
 * The place of the monitor while no pass runs (§ 2.2): the store receives nothing, whether
 * promoted patches open a pass by themselves, the last pass and its detail, what the served
 * epoch covers with « Les compléter » when it has gaps, and « Lancer une passe ». Without a
 * configured host it only says so: nothing can start.
 */
@Component({
  selector: 'lodb-idle-card',
  imports: [AdminCard, AdminTextPipe, Button],
  template: `
    <lodb-admin-card [heading]="'admin.prerender.overview.idle.title' | adminText" data-idle>
      <div class="flex flex-col gap-2 text-[0.9rem] text-text-muted">
        <p class="m-0">{{ lede() }}</p>
        @if (last(); as line) {
          <p class="m-0 flex flex-wrap items-center gap-x-3 gap-y-1" data-last-pass>
            <span>{{ line.text }}</span>
            <button
              lodbButton="ghost"
              lodbButtonSize="small"
              type="button"
              (click)="detail.emit(line.id)"
            >
              {{ 'admin.prerender.overview.idle.detail' | adminText }}
            </button>
          </p>
        } @else {
          <p class="m-0">{{ 'admin.prerender.overview.idle.never' | adminText }}</p>
        }
        @if (gaps(); as text) {
          <p class="m-0 flex flex-wrap items-center gap-x-3 gap-y-1" data-gaps>
            <span>{{ text }}</span>
            @if (canLaunch()) {
              <button
                lodbButton
                lodbButtonSize="small"
                type="button"
                (click)="launch.emit(gapsPreset)"
              >
                {{ 'admin.prerender.overview.idle.gaps_cta' | adminText }}
              </button>
            }
          </p>
        } @else if (isWhole()) {
          <p class="m-0" data-whole>{{ 'admin.prerender.overview.idle.whole' | adminText }}</p>
        }
      </div>
      @if (canLaunch()) {
        <div class="mt-4 flex justify-end max-sm:block">
          <button
            class="max-sm:w-full"
            lodbButton="gold"
            type="button"
            (click)="launch.emit(headerPreset)"
          >
            {{ 'admin.prerender.overview.cta.launch' | adminText }}
          </button>
        </div>
      }
    </lodb-admin-card>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdleCard {
  readonly report = input.required<PrerenderReport>();
  readonly lock = input.required<LaunchLock | null>();
  readonly launch = output<LaunchPreset>();
  readonly detail = output<number>();

  private readonly texts = injectAdminText();

  protected readonly headerPreset = entryPreset('header', null);
  // « Les compléter »: the gaps of the served epoch, resolved once the dialog reads them.
  protected readonly gapsPreset = entryPreset('idle', null);
  protected readonly canLaunch = computed(() => this.lock() === null);
  protected readonly lede = computed(() =>
    this.texts.text(`${IDLE}.lede`, {
      promotions: this.report().enqueuesPromotions ? 'on' : 'off',
    }),
  );
  protected readonly last = computed(() => {
    const [pass] = this.report().passes;
    if (!pass) {
      return null;
    }
    const text = this.texts.text(`${IDLE}.last`, {
      id: pass.id,
      state: this.texts.term('prerender.states', pass.state),
      date: stamp(pass.finishedAt ?? pass.createdAt, 'minute'),
      units: pass.units.total,
      pages: pass.pagesWritten,
    });
    return { id: pass.id, text };
  });
  private readonly coverage = computed(() => {
    const report = this.report();
    return report.storage.epochs.find((epoch) => epoch.epoch === report.servedEpoch)?.coverage;
  });
  protected readonly gaps = computed(() => {
    const coverage = this.coverage();
    if (!coverage || coverage.gapVersions === 0 || coverage.expectedUnits === 0) {
      return null;
    }
    return this.texts.text(`${IDLE}.gaps`, {
      share: figure(coverage.completeUnits / coverage.expectedUnits, 'share'),
      versions: coverage.gapVersions,
    });
  });
  protected readonly isWhole = computed(() => (this.coverage()?.expectedUnits ?? 0) > 0);
}
