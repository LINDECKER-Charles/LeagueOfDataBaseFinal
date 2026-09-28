import { Injectable, Injector, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { PlatformPolicy } from '../../../../api/generated/models/platform-policy';
import { ClientPolicyService } from '../../../../api/generated/services/client-policy.service';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import { LiveUpdates } from '../live/live-updates';
import type { NativeStep } from '../plan/native-step';
import { planUpdate } from '../plan/plan-update';
import { PlayUpdates } from '../play/play-updates';
import { TransitionalChannel } from '../transitional/transitional-channel';

const ANDROID = 'android';

/**
 * One check of the Android app: reads the shell, the bundles on the device and the client
 * policy, decides with `planUpdate`, then prepares the next bundle and the native update.
 * Built after the detection: the policy's client reads `API_BASE_URL`, hence `PLATFORM`.
 */
@Injectable({ providedIn: 'root' })
export class UpdateCheck {
  private readonly injector = inject(Injector);
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly live = inject(LiveUpdates);
  private readonly play = inject(PlayUpdates);
  private readonly transitional = inject(TransitionalChannel);

  /** Rejects when the device cannot be read; an unreadable policy changes nothing. */
  async run(): Promise<void> {
    const [policy, info, live] = await Promise.all([
      this.policy(),
      this.plugins.app.getInfo(),
      this.live.facts(),
    ]);
    if (policy === null) {
      return;
    }
    const plan = planUpdate({ nativeVersion: info.version, policy, ...live });
    await this.live.run(plan.live);
    await this.native(plan.native, Number(info.build));
  }

  // `/api/client-policy` answers even below the minimum: the API never refuses it with a 426.
  private async policy(): Promise<PlatformPolicy | null> {
    const service = this.injector.get(ClientPolicyService);
    const policy = await firstValueFrom(service.getClientPolicy()).catch(() => null);
    const platforms: unknown = policy?.platforms;
    return Array.isArray(platforms)
      ? ((platforms as PlatformPolicy[]).find((entry) => entry?.platform === ANDROID) ?? null)
      : null;
  }

  // Play first; the transitional channel only serves installations Play does not manage.
  private async native(step: NativeStep, versionCode: number): Promise<void> {
    if (step === 'none') {
      return;
    }
    if (!(await this.play.run(step))) {
      await this.transitional.run(versionCode);
    }
  }
}
