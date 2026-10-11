import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { PageContext } from '../../../../../core/context/page-context';
import { LOCALES } from '../../../../../core/i18n/locales';
import { ROOT_CATALOGUES } from '../../../../../core/testing/root-catalogues';
import { provideMeta } from '../../testing/provide-meta';
import { PreviousPatches } from './previous-patches';
import { previousVersionsOf } from './previous-versions-of';

const LATEST = '16.19.1';
const VERSIONS = [
  '16.20.1',
  LATEST,
  '16.18.1',
  '16.17.1',
  '16.16.1',
  '16.15.1',
  '16.14.1',
  '16.13.1',
  '16.12.1',
];
const ANNIE = 'champions/Annie';

let context: PageContext;

@Component({
  selector: 'lodb-host',
  imports: [PreviousPatches],
  template: '<lodb-previous-patches [context]="context" [path]="path" />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  protected readonly context = context;
  protected readonly path = ANNIE;
}

// The block of a page in `version`, at `url`, the versions of the stack being VERSIONS.
async function render(version: string, url = '/en/champions/Annie'): Promise<HTMLElement> {
  context = { locale: 'en', version, pinned: version !== LATEST, language: 'en_US' };
  TestBed.configureTestingModule({
    providers: [
      provideMeta({ latest: LATEST, versions: VERSIONS }),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: '**', component: Host }]),
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of(ROOT_CATALOGUES.en);
        },
      }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return harness.routeNativeElement as HTMLElement;
}

function hrefsOf(block: HTMLElement): (string | null)[] {
  return Array.from(block.querySelectorAll('nav a'), (link) => link.getAttribute('href'));
}

describe('PreviousPatches', () => {
  // The block reads `/api/meta` already answered for the page, and nothing else.
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('links the entry in the six versions listed before the latest one', async () => {
    const block = await render(LATEST);

    expect(block.querySelector('h2')?.textContent?.trim()).toBe('In previous patches');
    expect(hrefsOf(block)).toEqual(
      VERSIONS.slice(2, 8).map((version) => `/en/${version}/${ANNIE}`),
    );
    expect(Array.from(block.querySelectorAll('nav a'), (link) => link.textContent?.trim())).toEqual(
      VERSIONS.slice(2, 8),
    );
  });

  it('links the latest version by its short URL from a newer one', async () => {
    const block = await render('16.20.1');

    expect(hrefsOf(block)[0]).toBe(`/en/${ANNIE}`);
    expect(hrefsOf(block)).toHaveLength(6);
  });

  it('links the fewer versions an old patch has before it', async () => {
    const block = await render('16.14.1');

    expect(hrefsOf(block)).toEqual([`/en/16.13.1/${ANNIE}`, `/en/16.12.1/${ANNIE}`]);
  });

  it('carries the regional variant as a query, never escaped into the path', async () => {
    const block = await render(LATEST, '/en/champions/Annie?lang=en_GB');

    expect(hrefsOf(block)[0]).toBe(`/en/16.18.1/${ANNIE}?lang=en_GB`);
  });

  it('shows nothing on the oldest version listed', async () => {
    const block = await render('16.12.1');

    expect(block.querySelector('nav')).toBeNull();
  });
});

describe('previousVersionsOf', () => {
  it('lists none for a version the list does not hold', () => {
    expect(previousVersionsOf(VERSIONS, '9.9.9', 6)).toEqual([]);
  });
});

describe('title of the previous patches', () => {
  it.each(LOCALES)('is translated in %s', (locale) => {
    const detail = ROOT_CATALOGUES[locale]['detail'] as { history?: { title?: unknown } } | null;

    expect(detail?.history?.title).toMatch(/\S/);
  });
});
