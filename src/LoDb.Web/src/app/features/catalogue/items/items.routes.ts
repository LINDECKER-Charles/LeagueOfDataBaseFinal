import type { Routes } from '@angular/router';
import { resolveCatalogueContext } from '../../../core/routing/catalogue/resolve-catalogue-context';
import { resolveCatalogueEntry } from '../../../core/routing/catalogue/resolve-catalogue-entry';
import { withLoadTiming } from '../shared/codex/timing/with-load-timing';

const loadPage = () => import('./items-page').then((m) => m.ItemsPage);

/**
 * The item list and details, mounted twice by app.routes.ts: at `items` for the latest
 * version, and under a pinned `:version`. The resolvers of core/routing decide the canonical
 * URL, the status and the cache of the page; it reads what they resolved as `context` or
 * `entry`. They rerun on a query change, since `?lang=` and `?version=` change the page.
 * The detail's resolver is timed, for the `Server-Timing` header and the load-time badge.
 */
export const ITEMS_ROUTES: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { context: resolveCatalogueContext('items') },
    loadComponent: loadPage,
  },
  {
    path: ':id',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { entry: withLoadTiming(resolveCatalogueEntry('items')) },
    loadComponent: loadPage,
  },
];
