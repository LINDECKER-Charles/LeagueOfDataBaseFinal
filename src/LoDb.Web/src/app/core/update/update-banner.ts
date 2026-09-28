import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { Button } from '../../ui/controls/button';
import { PLATFORM } from '../platform/platform';
import { ClientUpdate } from './client-update';
import { UpdateRestart } from './update-restart';

/**
 * "Update ready, restart" banner of the apps (ADR 0008), in the `banner` slot: shown once
 * `PlatformService.updateState` is `ready`, until the user restarts or puts it off. Never on
 * the web, whose state stays `none`, and not under the blocking screen, which offers the
 * same restart. Its texts (`public/i18n/update/`) load only when it shows.
 */
@Component({
  selector: 'lodb-update-banner',
  imports: [Button, TranslocoPipe],
  providers: [provideTranslocoScope('update'), UpdateRestart],
  template: `@if (visible()) {
    <div
      class="flex flex-wrap items-center justify-center gap-3 border-b border-gold-deep bg-panel px-4 py-2.5 text-center text-sm text-gold-bright"
      role="region"
      [attr.aria-label]="'update.banner_label' | transloco"
    >
      <span role="status">{{ 'update.banner_ready' | transloco }}</span>
      <button type="button" lodbButton [disabled]="restart.busy()" (click)="restart.restart()">
        {{ 'update.restart' | transloco }}
      </button>
      <button type="button" lodbButton="ghost" (click)="dismissed.set(true)">
        {{ 'update.later' | transloco }}
      </button>
      @if (restart.failed()) {
        <span role="alert">{{ 'update.restart_failed' | transloco }}</span>
      }
    </div>
  }`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateBanner {
  private readonly platform = inject(PLATFORM, { optional: true });
  private readonly update = inject(ClientUpdate);
  private readonly blocked = computed(() => this.update.requirement() !== null);
  private readonly state = computed(() => this.platform?.updateState() ?? 'none');
  protected readonly restart = inject(UpdateRestart);
  // Put off for this update only: a later state, such as a newer download, shows it again.
  protected readonly dismissed = linkedSignal({ source: this.state, computation: () => false });
  protected readonly visible = computed(
    () => this.state() === 'ready' && !this.dismissed() && !this.blocked(),
  );
}
