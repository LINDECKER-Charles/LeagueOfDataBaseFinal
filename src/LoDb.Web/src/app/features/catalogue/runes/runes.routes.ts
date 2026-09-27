import type { Routes } from '@angular/router';
import { resolveCatalogueContext } from '../../../core/routing/catalogue/resolve-catalogue-context';
import { resolveCatalogueEntry } from '../../../core/routing/catalogue/resolve-catalogue-entry';
import { withLoadTiming } from '../shared/codex/timing/with-load-timing';

const loadPage = () => import('./runes-page').then((m) => m.RunesPage);

/**
 * The rune list and rune path details, mounted twice by app.routes.ts: at `runes` for the
 * latest version, and under a pinned `:version`. The resolvers of core/routing decide the
 * canonical URL, the status and the cache of the page; it reads what they resolved as
 * `context` or `entry`. They rerun on a query change, since `?lang=` and `?version=` change
 * the page.
 * The detail's resolver is timed, for the `Server-Timing` header and the load-time badge.
 */
export const RUNES_ROUTES: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { context: resolveCatalogueContext('runes') },
    loadComponent: loadPage,
  },
  {
    path: ':id',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { entry: withLoadTiming(resolveCatalogueEntry('runes')) },
    loadComponent: loadPage,
  },
];
