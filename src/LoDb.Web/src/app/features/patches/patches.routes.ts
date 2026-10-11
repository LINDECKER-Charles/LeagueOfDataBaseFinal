import type { Routes } from '@angular/router';
import { resolvePatchHistory } from './loading/resolve-patch-history';

/**
 * The patch history, `/{locale}/patches`, rendered by the server: every version Data Dragon
 * lists, each linking its four lists, so that crawlers reach any past patch through links.
 */
export const PATCHES_ROUTES: Routes = [
  {
    path: '',
    resolve: { history: resolvePatchHistory },
    loadComponent: () => import('./patch-history-page').then((m) => m.PatchHistoryPage),
  },
];
