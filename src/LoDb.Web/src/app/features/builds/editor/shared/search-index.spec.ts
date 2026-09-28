import { SearchIndex } from './search-index';

interface Named {
  readonly id: string;
  readonly name: string;
}

const CHAMPIONS: Named[] = [
  { id: 'Kaisa', name: "Kai'Sa" },
  { id: 'Seraphine', name: 'Séraphine' },
  { id: 'MonkeyKing', name: 'Wukong' },
];

describe('SearchIndex', () => {
  const index = new SearchIndex(CHAMPIONS, (champion) => `${champion.name} ${champion.id}`);
  const ids = (found: Named[]) => found.map((champion) => champion.id);

  it('keeps everything for a blank query', () => {
    expect(ids(index.matching('  '))).toEqual(['Kaisa', 'Seraphine', 'MonkeyKing']);
  });

  it('finds a name whatever the case and the accents', () => {
    expect(ids(index.matching('SERAPH'))).toEqual(['Seraphine']);
    expect(ids(index.matching('séra'))).toEqual(['Seraphine']);
  });

  it('finds a value by its id too', () => {
    expect(ids(index.matching('kaisa'))).toEqual(['Kaisa']);
    expect(ids(index.matching('monkey'))).toEqual(['MonkeyKing']);
  });

  it('keeps only the values the filter accepts', () => {
    expect(ids(index.matching('', (champion) => champion.id !== 'Kaisa'))).toEqual([
      'Seraphine',
      'MonkeyKing',
    ]);
  });
});
