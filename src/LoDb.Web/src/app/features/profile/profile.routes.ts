import type { Routes } from '@angular/router';

/** The public profile, `/{locale}/u/{username}`, rendered by the server for everyone. */
export const PROFILE_ROUTES: Routes = [
  {
    path: ':username',
    loadComponent: () => import('./profile-page').then((m) => m.ProfilePage),
  },
];
