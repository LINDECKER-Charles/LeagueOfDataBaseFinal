import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { RuneCard } from '../../../../core/api/generated/models/rune-card';
import type { PageContext } from '../../../../core/context/page-context';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { CatalogueLists } from '../../shared/data/catalogue-lists';
import { RuneList } from './rune-list';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_GB' };
const IMAGE: CatalogImage = { status: 'present', url: '/cdn/blobs/a.png' };
const TREE = { canonicalPath: 'runes/precision', id: 8000, image: IMAGE, key: 'Precision' };
const CARDS: RuneCard[] = [
  { slot: 'keystone', key: 'PressTheAttack', id: 8005 },
  { slot: 'row2', key: 'LegendAlacrity', id: 9104 },
].map((rune) => ({
  ...rune,
  canonicalPath: TREE.canonicalPath,
  image: IMAGE,
  name: rune.key,
  shortDesc: `<b>${rune.key}</b>`,
  tree: TREE.key,
}));
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => key,
};

async function render() {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  const list = { entries: CARDS, total: CARDS.length, trees: [{ ...TREE, name: 'Précision' }] };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/runes', component: RuneList, data: { context: CONTEXT } }]),
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: CatalogueHead, useValue: { write } },
      {
        provide: CatalogueLists,
        useValue: { fetch: () => of({ kind: 'list', list, retryAfterMs: null }) },
      },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/en/runes?lang=en_GB');
  await harness.fixture.whenStable();
  return { element: harness.routeNativeElement as HTMLElement, heads };
}

describe('lodb-rune-list', () => {
  it('bands the paths, and links each rune to its card on its path page', async () => {
    const { element } = await render();

    expect(element.querySelector('nav a')?.getAttribute('href')).toBe(
      '/en/runes/precision?lang=en_GB',
    );
    const links = [...element.querySelectorAll('lodb-rune-card > a')];
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/en/runes/precision?lang=en_GB#rune-PressTheAttack',
      '/en/runes/precision?lang=en_GB#rune-LegendAlacrity',
    ]);
  });

  it('names the path and the row of each rune, and renders its summary', async () => {
    const { element } = await render();

    const captions = [...element.querySelectorAll('lodb-rune-card p')].map((p) =>
      p.textContent?.trim(),
    );
    expect(captions).toEqual([
      'Précision · facet.rune.slot_keystone',
      'Précision · facet.rune.slot_row',
    ]);
    expect(element.querySelector('lodb-rune-card b')?.textContent).toBe('PressTheAttack');
  });

  it('counts the paths in its head, the pages it links to', async () => {
    const { heads } = await render();

    expect(heads.at(-1)?.description).toBe('rune.list.description {"count":1,"version":"16.19.1"}');
  });
});
