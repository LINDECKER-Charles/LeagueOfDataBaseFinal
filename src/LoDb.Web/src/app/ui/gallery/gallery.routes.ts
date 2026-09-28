import type { Routes } from '@angular/router';

/** Development gallery, mounted by app.routes.ts in development builds only. */
export const GALLERY_ROUTES: Routes = [
  {
    path: '',
    title: 'Hextech gallery',
    loadComponent: () => import('./gallery-page').then((m) => m.GalleryPage),
  },
];
