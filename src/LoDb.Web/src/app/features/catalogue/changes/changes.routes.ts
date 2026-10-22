import type { Routes } from '@angular/router';
import { AUDIENCE_ROUTE_DATA } from '../../../core/analytics/audience/audience-route-data';
import { resolvePatchChanges } from './loading/resolve-patch-changes';

/**
 * The page of what a patch changed, mounted twice by app.routes.ts: at `changes` for the
 * latest patch, and under a pinned `:version`. Its resolver decides the canonical URL, the
 * status and the cache, as for a list; it reruns on a query change (`?version=`, `?lang=`).
 * Its address is public: the audience receives its real path.
 */
export const CHANGES_ROUTES: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    data: AUDIENCE_ROUTE_DATA.path,
    resolve: { changes: resolvePatchChanges },
    loadComponent: () => import('./patch-changes-page').then((m) => m.PatchChangesPage),
  },
];
