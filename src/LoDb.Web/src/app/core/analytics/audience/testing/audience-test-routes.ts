import { Component } from '@angular/core';
import type { ResolveFn, Routes } from '@angular/router';
import type { PageOutcome } from '../../../routing/outcome/page-outcome';
import { AUDIENCE_ROUTE_DATA } from '../audience-route-data';

@Component({ template: '' })
class Probe {}

const notFound: ResolveFn<PageOutcome> = () => ({ kind: 'not-found' });
const redirected: ResolveFn<PageOutcome> = () => ({
  kind: 'redirect',
  status: 301,
  location: '/fr/items',
});

/**
 * The shapes of app.routes.ts the audience tells apart, without their lazy chunks: lists and
 * details marked `audience: 'path'`, under the locale and under a pinned version; a profile
 * and a shared build sent as templates; account and admin mounts never measured; a page and
 * its outcomes (404, redirect).
 */
export const AUDIENCE_TEST_ROUTES: Routes = [
  // First: without app.routes.ts's locale guard, `:locale` would take them.
  { path: 'b/:token', component: Probe },
  {
    path: 'admin',
    data: { chrome: 'bare', ...AUDIENCE_ROUTE_DATA.none },
    children: [{ path: 'users', component: Probe }],
  },
  {
    path: ':locale',
    children: [
      {
        path: 'champions',
        children: [
          { path: '', data: AUDIENCE_ROUTE_DATA.path, component: Probe },
          { path: ':id', data: AUDIENCE_ROUTE_DATA.path, component: Probe },
        ],
      },
      { path: 'u', children: [{ path: ':username', component: Probe }] },
      {
        path: 'account',
        data: AUDIENCE_ROUTE_DATA.none,
        children: [{ path: 'reset-password/:token', component: Probe }],
      },
      { path: 'legal/privacy', component: Probe },
      { path: 'old-items', resolve: { outcome: redirected }, component: Probe },
      { path: '', pathMatch: 'full', component: Probe },
      {
        path: ':version',
        children: [{ path: 'items/:id', data: AUDIENCE_ROUTE_DATA.path, component: Probe }],
      },
      { path: '**', resolve: { outcome: notFound }, component: Probe },
    ],
  },
];
