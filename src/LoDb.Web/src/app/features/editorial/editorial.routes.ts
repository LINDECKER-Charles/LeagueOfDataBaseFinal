import type { Route, Routes } from '@angular/router';
import type { LegalPageId } from './legal/legal-page-id';

const loadLegalPage = () => import('./legal/legal-page').then((m) => m.LegalPage);

// The four legal pages share one component: the route names the text it shows.
function legalRoute(page: LegalPageId): Route {
  return {
    path: `legal/${page}`,
    data: { heading: `legal.${page}.title`, legalPage: page },
    loadComponent: loadLegalPage,
  };
}

/**
 * The editorial pages, mounted at the locale root. app.routes.server.ts prerenders each of
 * these paths for the 21 locales: a path added here is added to its list, or it renders per
 * request. None of them calls the API while rendering; `heading` is the key of the page's
 * `<h1>`.
 */
export const EDITORIAL_ROUTES: Routes = [
  {
    path: 'about',
    data: { heading: 'about.index.title' },
    loadComponent: () => import('./about/about-page').then((m) => m.AboutPage),
  },
  {
    path: 'about/data',
    data: { heading: 'about.data.title' },
    loadComponent: () => import('./about/about-data-page').then((m) => m.AboutDataPage),
  },
  {
    path: 'faq',
    data: { heading: 'about.faq.title' },
    loadComponent: () => import('./about/faq-page').then((m) => m.FaqPage),
  },
  {
    path: 'changelog',
    data: { heading: 'changelog.title' },
    loadComponent: () => import('./changelog/changelog-page').then((m) => m.ChangelogPage),
  },
  legalRoute('notice'),
  legalRoute('privacy'),
  legalRoute('terms'),
  legalRoute('cookies'),
];
