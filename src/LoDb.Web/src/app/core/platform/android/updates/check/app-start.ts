import { Injectable, Injector, inject } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';
import { ActivePlatform } from '../../../detection/active-platform';

/**
 * The moment a bundle has proven it starts: the router begins its first navigation once the
 * root component is created and rendered. It waits for no network, so a slow API never makes
 * a sound bundle look faulty, while a bundle that fails to bootstrap never gets there.
 */
@Injectable({ providedIn: 'root' })
export class AppStart {
  private readonly injector = inject(Injector);

  async whenStarted(): Promise<void> {
    // The router is built after the detection: its dependencies may read `PLATFORM`.
    await this.injector.get(ActivePlatform).detect();
    const router = this.injector.get(Router);
    if (router.navigated || router.currentNavigation() !== null) {
      return;
    }
    await firstValueFrom(router.events.pipe(filter((event) => event instanceof NavigationStart)));
  }
}
