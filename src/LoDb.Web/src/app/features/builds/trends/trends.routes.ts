import type { Routes } from '@angular/router';
import { resolveTrends } from './loading/resolve-trends';

/**
 * The trending builds, `/{locale}/trends`, rendered by the server. The filters and the page
 * live in the query (`?champion=&mode=&language=&page=`): the resolver reruns when it changes.
 */
export const TRENDS_ROUTES: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { trends: resolveTrends },
    loadComponent: () => import('./trends-page').then((m) => m.TrendsPage),
  },
];
