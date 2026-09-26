import type { Routes } from '@angular/router';

const loadPage = () => import('./editor-page').then((m) => m.EditorPage);

/**
 * The builds of an account, `/{locale}/account/builds/...`: the list, the editor and the
 * import to another patch, rendered in the browser only (app.routes.server.ts).
 */
export const EDITOR_ROUTES: Routes = [
  { path: '', data: { heading: 'build.list.title' }, loadComponent: loadPage },
  { path: 'new', data: { heading: 'build.editor.title_create' }, loadComponent: loadPage },
  { path: ':id/edit', data: { heading: 'build.editor.title_edit' }, loadComponent: loadPage },
  { path: ':id/import', data: { heading: 'build.import.action' }, loadComponent: loadPage },
];
