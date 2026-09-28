import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionCard as Card } from '../../../../core/api/generated/models/champion-card';
import { ChampionCard } from './champion-card';
import { CHAMPION_CARD_ADAPTER } from './champion-card-adapter';
import { championFacets } from './champion-facets';
import { resourceOptions } from './resource-options';

function cardOf(overrides: Partial<Card> = {}): Card {
  return {
    canonicalPath: 'champions/Annie',
    id: 'Annie',
    key: '1',
    name: 'Annie',
    title: 'the Dark Child',
    image: { status: 'absent' },
    loadingArt: 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Annie_0.jpg',
    blurb: 'Dangerous, yet disarmingly precocious.',
    partype: 'Mana',
    resource: 'mana',
    attackRange: 'ranged',
    ratings: { attack: 2, defense: 3, magic: 10, difficulty: 6 },
    stats: [
      { stat: 'health', base: 560, perLevel: 96 },
      { stat: 'attack_speed', base: 0.61, perLevel: 1.36 },
      { stat: 'move_speed', base: 335, perLevel: null },
    ],
    tags: ['Mage', 'Support'],
    ...overrides,
  };
}

describe('CHAMPION_CARD_ADAPTER', () => {
  it('searches the name and the Data Dragon id, and keys a card by its path', () => {
    const card = cardOf({ name: 'Nunu & Willump', id: 'Nunu' });
    expect(CHAMPION_CARD_ADAPTER.searchTextOf(card)).toBe('Nunu & Willump Nunu');
    expect(CHAMPION_CARD_ADAPTER.keyOf(card)).toBe('champions/Annie');
  });

  it('gives the facets the roles, resource, range, ratings and base stats', () => {
    expect(CHAMPION_CARD_ADAPTER.valuesOf(cardOf())).toEqual({
      role: ['Mage', 'Support'],
      resource: ['mana'],
      range: ['ranged'],
      attack: 2,
      defense: 3,
      magic: 10,
      difficulty: 6,
      hp: 560,
      as: 0.61,
      ms: 335,
    });
  });

  it('leaves out what a champion lacks, and rounds a stat to three decimals', () => {
    const card = cardOf({
      attackRange: null,
      ratings: null,
      stats: [{ stat: 'attack_speed', base: 0.6250001, perLevel: 2 }],
    });
    expect(CHAMPION_CARD_ADAPTER.valuesOf(card)).toEqual({
      role: ['Mage', 'Support'],
      resource: ['mana'],
      as: 0.625,
    });
  });
});

describe('resourceOptions', () => {
  const cards = [
    cardOf({ resource: 'energy', partype: 'Energy' }),
    cardOf({ resource: 'mana', partype: 'Mana' }),
    cardOf({ resource: 'mana', partype: 'Mana' }),
    cardOf({ resource: 'none', partype: 'None' }),
    cardOf({ resource: 'fury', partype: ' ' }),
  ];

  it('offers the resources most spent first, named as the game names them', () => {
    expect(resourceOptions(cards, 'Resourceless')).toEqual([
      { value: 'mana', label: 'Mana' },
      { value: 'energy', label: 'Energy' },
      { value: 'none', label: 'Resourceless' },
      { value: 'fury', label: 'fury' },
    ]);
  });
});

describe('championFacets', () => {
  const translate = (key: string) => `<${key}>`;
  const facets = championFacets(translate, [cardOf()]);
  const facet = (key: string) => facets.find((candidate) => candidate.key === key);

  it('lists the profile choices, then the ratings, then the base stats', () => {
    expect(facets.map((candidate) => candidate.key)).toEqual([
      'role',
      'resource',
      'range',
      'difficulty',
      'attack',
      'defense',
      'magic',
      'hp',
      'armor',
      'mr',
      'ad',
      'as',
      'ms',
    ]);
    expect(
      facets.filter((candidate) => candidate.primary).map((candidate) => candidate.key),
    ).toEqual(['role', 'resource', 'range']);
  });

  it('labels the six roles, keeping the values the API gives', () => {
    expect(facet('role')?.options.map((option) => option.value)).toEqual([
      'Fighter',
      'Tank',
      'Mage',
      'Assassin',
      'Marksman',
      'Support',
    ]);
    expect(facet('role')?.options[0]?.label).toBe('<facet.champion.roles.fighter>');
    expect(facet('range')?.options.map((option) => option.label)).toEqual([
      '<facet.champion.ranges.melee>',
      '<facet.champion.ranges.ranged>',
    ]);
  });

  it('ranges the ratings and stats in their groups, the attack speed in hundredths', () => {
    expect(facet('difficulty')).toMatchObject({ kind: 'range', group: '<facet.group.ratings>' });
    expect(facet('hp')).toMatchObject({ kind: 'range', label: '<stat.health>', step: 1 });
    expect(facet('as')).toMatchObject({ group: '<facet.group.base_stats>', step: 0.01 });
  });
});

describe('lodb-champion-card', () => {
  async function render(card: Card) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTransloco({
          config: {
            availableLangs: ['en'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = () => of({ common: { detail: 'See details' } });
          },
        }),
      ],
    });
    const fixture = TestBed.createComponent(ChampionCard);
    fixture.componentRef.setInput('card', card);
    fixture.componentRef.setInput('href', '/en/champions/Annie');
    fixture.componentRef.setInput('locale', 'en');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('makes the whole card the link, with its capitalized title, roles and teaser', async () => {
    const element = await render(cardOf());
    const link = element.querySelector('a');

    expect(link?.getAttribute('href')).toBe('/en/champions/Annie');
    expect(link?.textContent).toContain('The Dark Child');
    const chips = [...element.querySelectorAll('.hx-chip')].map((chip) => chip.textContent);
    expect(chips).toEqual(['Mage', 'Support']);
    expect(link?.textContent).toContain('Dangerous, yet disarmingly precocious.');
    expect(link?.textContent).toContain('See details');
  });

  it('shows the loading-screen portrait of the champion', async () => {
    const element = await render(cardOf());
    const art = element.querySelector('img');
    expect(art?.getAttribute('src')).toBe(
      'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Annie_0.jpg',
    );
    expect(art?.getAttribute('alt')).toBe('Annie');
  });
});
