import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, inject } from '@angular/core';
import type { PreloadingStrategy, Route } from '@angular/router';
import { Observable, of, switchMap } from 'rxjs';
import { connectionOf } from '../network/connection-of';
import { wantsLightData } from '../network/wants-light-data';
import { whenIdleAfterLoad } from '../network/when-idle-after-load';
import { PRELOAD_KEY } from './preload-key';

/**
 * The router's preloading: the code of the routes marked PRELOADED_ROUTE (the catalogue), read
 * once the page has loaded and the browser is idle, so the next list or page opens without
 * waiting for its chunk. Never another section (account, editor, admin), never on the server,
 * never under Save-Data or on a slow connection.
 */
@Injectable({ providedIn: 'root' })
export class CataloguePreloading implements PreloadingStrategy {
  private readonly view = inject(DOCUMENT).defaultView;
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
    const view = this.view;
    if (route.data?.[PRELOAD_KEY] !== true || !this.isBrowser || view === null) {
      return of(null);
    }
    if (wantsLightData(connectionOf(view))) {
      return of(null);
    }
    const idle = new Observable<void>((subscriber) =>
      whenIdleAfterLoad(view, () => {
        subscriber.next();
        subscriber.complete();
      }),
    );
    return idle.pipe(switchMap(load));
  }
}
