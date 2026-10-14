import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import type { CanonicalLink } from '../../../../../core/api/generated/models/canonical-link';
import type { PageContext } from '../../../../../core/context/page-context';
import { provideTestTransloco } from '../../../../../core/testing/provide-test-transloco';
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
let canonical: CanonicalLink | null | undefined;

@Component({
  selector: 'lodb-host',
  imports: [PreviousPatches],
  template: `<lodb-previous-patches [context]="context" [path]="path" [canonical]="canonical" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  protected readonly context = context;
  protected readonly path = ANNIE;
  protected readonly canonical = canonical;
}

// The block of a page in `version`, at `url`, the versions of the stack being VERSIONS, its
// entity indexed at `indexedAt`.
async function render(
  version: string,
  url = '/en/champions/Annie',
  indexedAt?: CanonicalLink | null,
): Promise<HTMLElement> {
  context = { locale: 'en', version, pinned: version !== LATEST, language: 'en_US' };
  canonical = indexedAt;
  TestBed.configureTestingModule({
    providers: [
      provideMeta({ latest: LATEST, versions: VERSIONS }),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: '**', component: Host }]),
      provideTestTransloco({ otherwise: ROOT_CATALOGUES.en }),
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

describe('PreviousPatches, at the latest patch', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const AT_LATEST: CanonicalLink = { version: null, path: 'champions/Annie' };

  function latestLinkOf(block: HTMLElement): HTMLAnchorElement | null {
    return block.querySelector('p a');
  }

  it('links a pinned page to its canonical at the latest patch', async () => {
    const block = await render('16.14.1', '/en/16.14.1/champions/Annie', AT_LATEST);

    expect(block.querySelector('p')?.textContent).toContain('In the latest patch');
    expect(latestLinkOf(block)?.getAttribute('href')).toBe(`/en/${ANNIE}`);
    expect(latestLinkOf(block)?.textContent?.trim()).toBe(LATEST);
  });

  it('links it from the oldest version listed, which has no previous patch', async () => {
    const block = await render('16.12.1', '/en/16.12.1/champions/Annie', AT_LATEST);

    expect(latestLinkOf(block)?.getAttribute('href')).toBe(`/en/${ANNIE}`);
    expect(block.querySelector('nav')).toBeNull();
  });

  it('follows the path the API gives, a renamed entry keeping its id', async () => {
    const renamed = { version: null, path: 'items/3082-wardens-mail' };
    const block = await render('16.14.1', '/en/16.14.1/items/3082-warden-mail', renamed);

    expect(latestLinkOf(block)?.getAttribute('href')).toBe('/en/items/3082-wardens-mail');
  });

  it('carries the regional variant on', async () => {
    const block = await render('16.14.1', '/en/16.14.1/champions/Annie?lang=en_GB', AT_LATEST);

    expect(latestLinkOf(block)?.getAttribute('href')).toBe(`/en/${ANNIE}?lang=en_GB`);
  });

  it('links nothing for an entity retired from the game', async () => {
    const retired = { version: '16.13.1', path: ANNIE };
    const block = await render('16.14.1', '/en/16.14.1/champions/Annie', retired);

    expect(latestLinkOf(block)).toBeNull();
  });

  it('links nothing for a page out of the index', async () => {
    const block = await render('16.14.1', '/en/16.14.1/champions/Annie', null);

    expect(latestLinkOf(block)).toBeNull();
  });

  it('links nothing from the latest patch itself', async () => {
    const block = await render(LATEST, '/en/champions/Annie', AT_LATEST);

    expect(latestLinkOf(block)).toBeNull();
  });
});

describe('previousVersionsOf', () => {
  it('lists none for a version the list does not hold', () => {
    expect(previousVersionsOf(VERSIONS, '9.9.9', 6)).toEqual([]);
  });
});
