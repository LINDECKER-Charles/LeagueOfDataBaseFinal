import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  PLATFORM_ID,
  REQUEST_CONTEXT,
  TransferState,
  makeStateKey,
} from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { ApiMeta } from '../../api/meta/api-meta';
import type { NavSelection } from '../../context/nav/nav-selection';
import { NavContext } from './nav-context';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Page {}

const META: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.19.1', '14.1.1'],
  readyVersions: ['16.19.1'],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [{ locale: 'en', language: 'en_US' }],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
const PINNED: NavSelection = { shown: '14.1.1', version: '14.1.1', lang: null };
const RENDERED = makeStateKey<NavSelection | null>('lodb.nav.selection');

interface Setup {
  readonly platform: 'browser' | 'server';
  /** A server render of a request, rather than a prerender. */
  readonly request?: boolean;
  /** What the server handed over in the page. */
  readonly rendered?: NavSelection;
}

// The service on `/en/14.1.1/champions`, with an `/api/meta` the spec answers.
async function navContextOn({ platform, request = false, rendered }: Setup) {
  const meta = new Subject<CatalogMeta>();
  const requests = { count: 0 };
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: platform },
      request ? { provide: REQUEST_CONTEXT, useValue: { apiOrigin: '', selfOrigin: null } } : [],
      provideRouter([{ path: '**', component: Page }]),
      { provide: ApiMeta, useValue: { meta: () => (requests.count++, meta) } },
    ],
  });
  if (rendered !== undefined) {
    TestBed.inject(TransferState).set(RENDERED, rendered);
  }
  await TestBed.inject(Router).navigateByUrl('/en/14.1.1/champions');
  const nav = TestBed.inject(NavContext);
  return { nav, meta, requests };
}

describe('NavContext', () => {
  it('reads /api/meta for a server render, and hands its selection to the browser', async () => {
    const { nav, meta } = await navContextOn({ platform: 'server', request: true });

    meta.next(META);

    expect(nav.selection()).toEqual(PINNED);
    expect(JSON.parse(TestBed.inject(TransferState).toJson())).toEqual({
      'lodb.nav.selection': PINNED,
    });
  });

  it('reads nothing while prerendering: a prerendered page names no context', async () => {
    const { nav, requests } = await navContextOn({ platform: 'server' });

    expect(requests.count).toBe(0);
    expect(nav.selection()).toBeNull();
  });

  it("starts the browser from the server's selection, until the options load", async () => {
    const { nav, meta } = await navContextOn({ platform: 'browser', rendered: PINNED });

    expect(nav.selection()).toEqual(PINNED);

    await TestBed.inject(Router).navigateByUrl('/en/trends');
    expect(nav.selection()).toBeNull();
    // The options load after the first render.
    TestBed.inject(ApplicationRef).tick();
    meta.next(META);
    expect(nav.selection()).toEqual({ shown: '16.19.1', version: null, lang: null });
  });
});
