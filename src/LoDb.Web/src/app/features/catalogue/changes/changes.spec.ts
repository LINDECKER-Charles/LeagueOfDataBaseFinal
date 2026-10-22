import {
  HttpErrorResponse,
  HttpHeaders,
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  Router,
  type Routes,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { PatchChanges } from '../../../core/api/generated/models/patch-changes';
import { localeGuard } from '../../../core/i18n/locale-guard';
import { handleNavigationError } from '../../../core/routing/failure/handle-navigation-error';
import { isOutcomeNavigation } from '../../../core/routing/outcome/is-outcome-navigation';
import { resolveOutcome } from '../../../core/routing/outcome/resolve-outcome';
import { canMatchVersion } from '../../../core/routing/version/can-match-version';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Seo } from '../../../core/seo/seo';
import { catalogMetaOf } from '../../../core/testing/catalog-meta-of';
import { PATCHES_CATALOGUES } from '../../../core/testing/patches-catalogues';
import { provideTestTransloco } from '../../../core/testing/provide-test-transloco';
import { ROOT_CATALOGUES } from '../../../core/testing/root-catalogues';
import { CHANGES_ROUTES } from './changes.routes';

@Component({ template: '' })
class Probe {}

const LATEST = '16.20.1';
const OLDER = '16.19.1';
const OLDEST = '16.18.1';
const META = catalogMetaOf({
  latest: LATEST,
  versions: [LATEST, OLDER, OLDEST],
  oldestChangesVersion: OLDER,
  languages: ['fr_FR'],
  locales: [{ locale: 'fr', language: 'fr_FR' }],
});
const TRANSIENT = 'public, max-age=0, s-maxage=60';
const IMAGE = { status: 'present', url: '/cdn/blobs/a.png' } as const;

// What OLDER changed: Ahri's Q and a new item, and a removed one, its opener elsewhere.
function changesOf(version: string, hasChanges = true): PatchChanges {
  return {
    version,
    language: 'fr_FR',
    previousVersion: OLDEST,
    hasChanges,
    groups: hasChanges
      ? [
          {
            type: 'champions',
            entries: [
              {
                id: 'Ahri',
                name: 'Ahri',
                image: IMAGE,
                kind: 'changed',
                changedFields: [
                  { field: 'spellCooldown', subject: { kind: 'spellSlot', key: 'q', name: null } },
                  { field: 'goldTotal', subject: null },
                ],
                link: { version, path: 'champions/Ahri' },
              },
            ],
          },
          {
            type: 'items',
            entries: [
              {
                id: '3371',
                name: 'Lame en fusion',
                image: IMAGE,
                kind: 'retired',
                changedFields: [],
                link: { version: '11.5.1', path: 'items/3371-molten-edge' },
              },
              {
                id: '6610',
                name: 'Fendoir',
                image: IMAGE,
                kind: 'new',
                changedFields: [],
                link: { version, path: 'items/6610-sundered-sky' },
              },
            ],
          },
        ]
      : [],
  };
}

interface Answer {
  readonly status: number;
  readonly body: unknown;
  readonly headers?: Record<string, string>;
}

const ANSWERS: Record<string, Answer> = {
  '/api/meta': { status: 200, body: META },
  [`/api/catalog/${OLDER}/fr_FR/changes`]: { status: 200, body: changesOf(OLDER) },
  [`/api/catalog/${LATEST}/fr_FR/changes`]: { status: 200, body: changesOf(LATEST, false) },
};
// OLDER still cold: its icons are placeholders, and the API says when to read it again.
const PENDING_ICONS: Record<string, Answer> = {
  ...ANSWERS,
  [`/api/catalog/${OLDER}/fr_FR/changes`]: {
    status: 200,
    body: changesOf(OLDER),
    headers: { 'Cache-Control': 'public, max-age=5', 'Retry-After': '5' },
  },
};
const NO_PATCH_PAGE = { status: 404, body: { status: 404, code: 'no-patch-page' } };

function simulatedApi(requested: string[], answers: Record<string, Answer>): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const { status, body, headers }: Answer = answers[url] ?? NO_PATCH_PAGE;
    const init = { status, url, headers: new HttpHeaders(headers) };
    return status < HttpStatusCode.BadRequest
      ? of(new HttpResponse({ ...init, body }))
      : throwError(() => new HttpErrorResponse({ ...init, error: body }));
  };
}

// The shape of app.routes.ts, reduced to the page of a patch's changes.
const ROUTES: Routes = [
  {
    path: ':locale',
    canMatch: [localeGuard],
    children: [
      {
        path: '**',
        canMatch: [isOutcomeNavigation],
        resolve: { outcome: resolveOutcome },
        component: Probe,
      },
      { path: 'changes', children: CHANGES_ROUTES },
      {
        path: ':version',
        canMatch: [canMatchVersion],
        children: [{ path: 'changes', children: CHANGES_ROUTES }],
      },
      { path: '**', resolve: { outcome: resolveOutcome }, component: Probe },
    ],
  },
];

async function visit(url: string, answers = ANSWERS) {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        ROUTES,
        withRouterConfig({
          paramsInheritanceStrategy: 'always',
          resolveNavigationPromiseOnError: true,
        }),
        withNavigationErrorHandler(handleNavigationError),
      ),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([simulatedApi(requested, answers)])),
      provideTestTransloco({
        lang: 'fr',
        byPath: { fr: ROOT_CATALOGUES.fr, 'patches/fr': PATCHES_CATALOGUES.fr },
      }),
      { provide: API_BASE_URL, useValue: '' },
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: RESPONSE_INIT, useValue: init },
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  let leaf: ActivatedRouteSnapshot = TestBed.inject(Router).routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  return { init, requested, leaf, page: harness.routeNativeElement as HTMLElement | null, apply };
}

function headerOf(init: ResponseInit, name: string): string | null {
  return new Headers(init.headers).get(name);
}

describe("the page of a patch's changes", () => {
  it('renders a past patch pinned in its path, frozen like a list of its version', async () => {
    const { init, requested, page } = await visit(`/fr/${OLDER}/changes`);

    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(headerOf(init, 'Cache-Control')).toBe('public, max-age=3600, s-maxage=31536000');
    expect(requested).toEqual(['/api/meta', `/api/catalog/${OLDER}/fr_FR/changes`]);
    expect(page?.querySelector('h1')?.textContent?.trim()).toBe(`Changements du patch ${OLDER}`);
  });

  it('keeps a past patch out of every store while its icons are placeholders', async () => {
    const { init, page } = await visit(`/fr/${OLDER}/changes`, PENDING_ICONS);

    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(headerOf(init, 'Cache-Control')).toBe(TRANSIENT);
    expect(page?.querySelector('h1')?.textContent?.trim()).toBe(`Changements du patch ${OLDER}`);
  });

  it('groups each resource by what the patch did, linking each page it names', async () => {
    const { page } = await visit(`/fr/${OLDER}/changes`);
    const sections = Array.from(page?.querySelectorAll('section') ?? []);
    const groups = sections.map((section) =>
      Array.from(section.querySelectorAll('h3'), (h3) => h3.textContent?.trim()),
    );
    const links = Array.from(page?.querySelectorAll('[role="listitem"] a') ?? [], (link) =>
      link.getAttribute('href'),
    );

    expect(sections.map((section) => section.querySelector('h2')?.textContent?.trim())).toEqual([
      'Champions',
      'Objets',
    ]);
    expect(groups).toEqual([['Modifiés'], ['Nouveaux', 'Retirés']]);
    expect(links).toEqual([
      `/fr/${OLDER}/champions/Ahri`,
      `/fr/${OLDER}/items/6610-sundered-sky`,
      '/fr/11.5.1/items/3371-molten-edge',
    ]);
    expect(page?.querySelector('[role="listitem"] p')?.textContent?.trim()).toBe(
      'Q : temps de recharge, Prix total',
    );
    const icons = page?.querySelectorAll('[role="listitem"] lodb-catalogue-image') ?? [];
    expect(Array.from(icons, (icon) => icon.getAttribute('aria-hidden'))).toEqual([
      'true',
      'true',
      'true',
    ]);
  });

  it('heads a past patch with its own canonical, indexable when it changed something', async () => {
    const { apply } = await visit(`/fr/${OLDER}/changes`);

    expect(apply.mock.lastCall?.[0]).toMatchObject({
      kind: 'page',
      path: 'changes',
      version: OLDER,
      canonical: { version: OLDER, path: 'changes' },
      titlePatches: false,
    });
  });

  it('serves the latest patch at its short URL, out of the index when it changed nothing', async () => {
    const { init, page, apply } = await visit('/fr/changes');

    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(headerOf(init, 'Cache-Control')).toBe(
      'public, max-age=0, s-maxage=1800, stale-while-revalidate=86400',
    );
    expect(page?.textContent).toContain('Ce patch n’a rien changé');
    expect(apply.mock.lastCall?.[0]).toMatchObject({ kind: 'noindex', version: null });
  });

  it.each([
    [`/fr/${LATEST}/changes`, '/fr/changes'],
    [`/fr/changes?version=${OLDER}`, `/fr/${OLDER}/changes`],
    [`/fr/changes?version=${LATEST}&lang=fr_FR`, '/fr/changes?lang=fr_FR'],
  ])('moves %s to %s with a 301, before any change is read', async (url, location) => {
    const { init, requested } = await visit(url);

    expect(init.status).toBe(HttpStatusCode.MovedPermanently);
    expect(headerOf(init, 'Location')).toBe(location);
    expect(headerOf(init, 'Cache-Control')).toBe(TRANSIENT);
    expect(requested).toEqual(['/api/meta']);
  });

  it.each([
    ['the first version the index covers, which the API has no page for', `/fr/${OLDEST}/changes`],
    ['a version Data Dragon does not list', '/fr/99.99.1/changes'],
  ])('answers a real 404 for %s', async (_case, url) => {
    const { init, leaf } = await visit(url);

    expect(leaf.data['outcome']).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect(headerOf(init, 'Cache-Control')).toBe(TRANSIENT);
  });
});
