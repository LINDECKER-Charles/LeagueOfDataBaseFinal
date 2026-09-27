import { isPlatformBrowser } from '@angular/common';
import {
  DestroyRef,
  Injectable,
  PLATFORM_ID,
  TransferState,
  afterNextRender,
  computed,
  inject,
  makeStateKey,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { ApiMeta } from '../../api/meta/api-meta';
import { injectSsrRequestContext } from '../../http/inject-ssr-request-context';
import { PageDirection } from '../direction/page-direction';
import { PreferencesStore } from '../../context/preferences/preferences-store';
import type { NavSelection } from '../../context/nav/nav-selection';
import { navSelectionOf } from '../../context/nav/nav-selection-of';

// The server's selection, handed to the browser so its first render links and names the
// same context before `/api/meta` is loaded again.
const RENDERED_SELECTION = makeStateKey<NavSelection | null>('lodb.nav.selection');

/**
 * The selection of the current page, for the chrome (header, footer, bottom bar, switcher).
 * A server render reads `/api/meta` itself, which its resolvers have usually requested
 * already; a prerendered page names no context, so the prerender reads nothing. The browser
 * loads it after its first render, which reuses the server's selection meanwhile. Null until
 * the context is known: the links then follow the latest version.
 */
@Injectable({ providedIn: 'root' })
export class NavContext {
  private readonly router = inject(Router);
  private readonly page = inject(PageDirection);
  private readonly preferences = inject(PreferencesStore);
  private readonly apiMeta = inject(ApiMeta);
  private readonly destroyRef = inject(DestroyRef);
  private readonly meta = signal<CatalogMeta | null>(null);
  private readonly firstUrl = this.router.url;
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.firstUrl },
  );
  private readonly rendered: NavSelection | null;

  readonly selection = computed<NavSelection | null>(() => {
    const meta = this.meta();
    const page = { url: this.url(), locale: this.page.locale() };
    if (meta === null) {
      return page.url === this.firstUrl ? this.rendered : null;
    }
    return navSelectionOf(page, meta, this.preferences.read());
  });

  constructor() {
    const state = inject(TransferState);
    const inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
    this.rendered = inBrowser ? state.get(RENDERED_SELECTION, null) : null;
    if (inBrowser) {
      afterNextRender(() => this.load());
    } else if (injectSsrRequestContext() !== null) {
      state.onSerialize(RENDERED_SELECTION, () => this.selection());
      this.load();
    }
  }

  private load(): void {
    this.apiMeta
      .meta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (meta) => this.meta.set(meta),
        // Unreachable: the links keep following the latest version.
        error: () => undefined,
      });
  }
}
