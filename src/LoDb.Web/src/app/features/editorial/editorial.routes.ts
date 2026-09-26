import type { Routes } from '@angular/router';

const loadPage = () => import('./editorial-page').then((m) => m.EditorialPage);

/**
 * The editorial pages, mounted at the locale root. app.routes.server.ts prerenders each of
 * these paths for the 21 locales: a path added here is added to its list, or it renders per
 * request. None of them may call the API while rendering.
 */
export const EDITORIAL_ROUTES: Routes = [
  { path: 'about', data: { heading: 'about.index.title' }, loadComponent: loadPage },
  { path: 'about/data', data: { heading: 'about.data.title' }, loadComponent: loadPage },
  { path: 'faq', data: { heading: 'about.faq.title' }, loadComponent: loadPage },
  { path: 'changelog', data: { heading: 'changelog.title' }, loadComponent: loadPage },
  { path: 'legal/notice', data: { heading: 'legal.notice.title' }, loadComponent: loadPage },
  { path: 'legal/privacy', data: { heading: 'legal.privacy.title' }, loadComponent: loadPage },
  { path: 'legal/terms', data: { heading: 'legal.terms.title' }, loadComponent: loadPage },
  { path: 'legal/cookies', data: { heading: 'legal.cookies.title' }, loadComponent: loadPage },
];
