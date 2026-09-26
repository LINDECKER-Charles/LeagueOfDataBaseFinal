import type { Routes } from '@angular/router';
import { resolveSharedBuild } from './loading/resolve-shared-build';

/**
 * A shared build, `/b/{token}`, outside the locales: rendered by the server in the build's
 * own language, always `noindex` (app.routes.server.ts). `?version=` and `?lang=` change what
 * it is compared with and named in: the resolver reruns when they change.
 */
export const SHARE_ROUTES: Routes = [
  {
    path: ':token',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { shared: resolveSharedBuild },
    loadComponent: () => import('./share-page').then((m) => m.SharePage),
  },
];
