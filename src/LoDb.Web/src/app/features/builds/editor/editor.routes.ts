import type { Routes } from '@angular/router';
import { authenticatedGuard } from '../../../core/auth/guards/authenticated-guard';
import { verifiedEmailGuard } from '../../../core/auth/guards/verified-email-guard';
import type { EditorView } from './editor-view';
import { resolveEditorEntry } from './entry/resolve-editor-entry';
import { browserOnly } from './shared/browser-only';

const loadPage = () => import('./editor-page').then((m) => m.EditorPage);
const forAccounts = [browserOnly(authenticatedGuard)];
// Creating a build, an import included, asks for a verified e-mail (the API's policy).
const forAuthors = [browserOnly(verifiedEmailGuard)];

// `heading` is the translation key of the page's title, `view` what EditorPage shows.
function view(view: EditorView, heading: string) {
  return { data: { heading, view }, loadComponent: loadPage };
}

const editor = { resolve: { entry: resolveEditorEntry } };

/**
 * The builds of an account, `/{locale}/account/builds/...`: the list, the editor and the
 * import to another patch (`?to=`), rendered in the browser only (app.routes.server.ts).
 * Another `?to=` resolves the import again.
 */
export const EDITOR_ROUTES: Routes = [
  { path: '', canActivate: forAccounts, ...view('mine', 'build.list.title') },
  {
    path: 'new',
    canActivate: forAuthors,
    ...editor,
    ...view('create', 'build.editor.title_create'),
  },
  {
    path: ':id/edit',
    canActivate: forAccounts,
    ...editor,
    ...view('edit', 'build.editor.title_edit'),
  },
  {
    path: ':id/import',
    canActivate: forAuthors,
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    ...editor,
    ...view('import', 'build.import.action'),
  },
];
