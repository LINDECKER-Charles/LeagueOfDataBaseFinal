import type { Routes } from '@angular/router';
import { resolvePublicProfile } from './loading/resolve-public-profile';

/**
 * The public profile, `/{locale}/u/{username}`, rendered on the server for anyone. `/u`
 * alone is app.routes.ts's 404. `?version=` and `?lang=` choose the context of its names:
 * the resolver reruns when they change.
 */
export const PROFILE_ROUTES: Routes = [
  {
    path: ':username',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { profile: resolvePublicProfile },
    loadComponent: () => import('./profile-page').then((m) => m.ProfilePage),
  },
];
