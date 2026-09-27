import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { RuneTreeCard } from '../../../../core/api/generated/models/rune-tree-card';
import type { RuneTreeDetails } from '../../../../core/api/generated/models/rune-tree-details';
import type { PageContext } from '../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { RuneDetail } from './rune-detail';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const IMAGE: CatalogImage = { status: 'present', url: '/cdn/blobs/domination.png' };
const TREES: RuneTreeCard[] = ['Precision', 'Domination', 'Sorcery'].map((key, id) => ({
  canonicalPath: `runes/${key.toLowerCase()}`,
  id,
  image: IMAGE,
  key,
  name: key,
}));
const DETAILS: RuneTreeDetails = {
  canonicalPath: 'runes/domination',
  language: 'en_US',
  version: CONTEXT.version,
  profile: TREES[1] as RuneTreeCard,
  neighbours: {
    previous: { id: '0', name: 'Precision', canonicalPath: 'runes/precision', edition: 'modern' },
    next: { id: '2', name: 'Sorcery', canonicalPath: 'runes/sorcery', edition: 'modern' },
  },
  slots: [
    {
      slot: 'keystone',
      runes: [
        {
          id: 8112,
          key: 'Electrocute',
          name: 'Electrocute',
          image: IMAGE,
          shortDesc: 'Burst',
          longDesc: '<b>Burst</b> damage',
        },
      ],
    },
    {
      slot: 'row1',
      runes: [
        {
          id: 8126,
          key: 'CheapShot',
          name: 'Cheap Shot',
          image: IMAGE,
          shortDesc: 'True damage',
          longDesc: 'Bonus true damage',
        },
      ],
    },
  ],
};
const ENTRY: CatalogueEntry<RuneTreeDetails> = { context: CONTEXT, details: DETAILS };
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => key,
};

async function render(entry = ENTRY) {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/runes/:id', component: RuneDetail, data: { entry } }]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: CatalogueHead, useValue: { write } },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/en/runes/domination');
  await harness.fixture.whenStable();
  return { element: harness.routeNativeElement as HTMLElement, heads };
}

describe('lodb-rune-detail', () => {
  beforeEach(() => {
    // Reduced motion: lodbReveal shows the sections at once, without an observer.
    vi.stubGlobal('matchMedia', () => ({ matches: true }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("tints the page in its path's colour and strings its constellation", async () => {
    const { element } = await render();

    expect(element.querySelector('.path-domination h1')?.textContent?.trim()).toBe('Domination');
    const cards = [...element.querySelectorAll('lodb-rune-constellation article')];
    expect(cards.map((card) => card.id)).toEqual(['rune-Electrocute', 'rune-CheapShot']);
    expect(element.querySelector('#rune-Electrocute b')?.textContent).toBe('Burst');
    expect(element.querySelector('#rune-CheapShot details')?.textContent).toContain(
      'Bonus true damage',
    );
  });

  it('turns the pages of the paths, not of the runes', async () => {
    const { element } = await render();

    expect(element.querySelector('lodb-pager a[rel="prev"]')?.getAttribute('href')).toBe(
      '/en/runes/precision',
    );
    expect(element.querySelector('lodb-pager a[rel="next"]')?.getAttribute('href')).toBe(
      '/en/runes/sorcery',
    );
  });

  it("previews the path's mark and describes it as a rune path", async () => {
    const { heads } = await render();

    expect(heads.at(-1)?.image).toBe('/cdn/blobs/domination.png');
    expect(heads.at(-1)?.title).toBe('rune.detail.title {"name":"Domination"}');
  });

  it('says so when the path has no runes', async () => {
    const { element } = await render({ ...ENTRY, details: { ...DETAILS, slots: [] } });

    expect(element.querySelector('lodb-rune-constellation')).toBeNull();
    expect(element.textContent).toContain('runes.empty');
  });
});
