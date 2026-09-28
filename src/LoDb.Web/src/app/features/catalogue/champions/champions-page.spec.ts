import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionCard } from '../../../core/api/generated/models/champion-card';
import type { PageContext } from '../../../core/context/page-context';
import { Seo } from '../../../core/seo/seo';
import type { SeoPage } from '../../../core/seo/seo-page';
import { CatalogueLists } from '../shared/data/catalogue-lists';
import type { ListRequest } from '../shared/data/list-request';
import { ChampionsPage } from './champions-page';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const EN = { champion: { list: { title: 'Champions' } } };

function cardOf(id: string, tags: string[]): ChampionCard {
  return {
    canonicalPath: `champions/${id}`,
    id,
    key: id,
    name: id,
    title: 'the Dark Child',
    image: { status: 'absent' },
    loadingArt: `https://ddragon.leagueoflegends.com/cdn/img/champion/loading/${id}_0.jpg`,
    blurb: 'Dangerous, yet disarmingly precocious.',
    partype: 'Mana',
    resource: 'mana',
    stats: [],
    tags,
  };
}

const ENTRIES = [cardOf('Ahri', ['Mage', 'Assassin']), cardOf('Annie', ['Mage'])];

async function visit() {
  const apply = vi.fn<(page: SeoPage) => Promise<void>>().mockResolvedValue(undefined);
  const requests: ListRequest[] = [];
  const fetch = (_: string, request: ListRequest) => {
    requests.push(request);
    const list = { entries: ENTRIES, total: ENTRIES.length, version: CONTEXT.version };
    return of({ kind: 'list', list, retryAfterMs: null });
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'en/champions', resolve: { context: () => CONTEXT }, component: ChampionsPage },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(path === 'en' ? EN : {});
        },
      }),
      { provide: Seo, useValue: { apply } },
      { provide: CatalogueLists, useValue: { fetch } },
    ],
  });
  const harness = await RouterTestingHarness.create('/en/champions');
  await harness.fixture.whenStable();
  return { apply, requests, host: harness.routeNativeElement as HTMLElement };
}

describe('ChampionsPage', () => {
  it('titles the list with its version, once, and its count', async () => {
    const { host } = await visit();

    expect(host.querySelector('h1')?.textContent?.trim()).toBe('Champions');
    expect(host.textContent?.split('16.19.1')).toHaveLength(2);
    expect(host.querySelector('.list-count')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      '2 champions',
    );
  });

  it('links a card per champion, with its roles', async () => {
    const { host } = await visit();
    const cards = [...host.querySelectorAll('lodb-champion-card')];

    expect(cards.map((card) => card.querySelector('a')?.getAttribute('href'))).toEqual([
      '/en/champions/Ahri',
      '/en/champions/Annie',
    ]);
    expect(cards[0]?.querySelectorAll('.hx-chip')).toHaveLength(2);
  });

  it('writes the head of the list, with the champions it opens on', async () => {
    const { apply } = await visit();

    expect(apply).toHaveBeenCalledWith(
      expect.objectContaining({ path: 'champions', image: '/preview/champions.png' }),
    );
  });
});
