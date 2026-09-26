import { filterOptions } from './filter-options';
import { normalizeSearchText } from './normalize-search-text';
import { flatEntriesOf } from './flat-entries-of';
import { runeEntriesOf } from './rune-entries-of';
import type { PickerEntry } from './picker-entry';

function entry(overrides: Partial<PickerEntry> & { id: string; name: string }): PickerEntry {
  return { image: null, searchText: normalizeSearchText(overrides.name), ...overrides };
}

const AHRI = entry({ id: 'Ahri', name: 'Ahri' });
const SERAPHINE = entry({ id: 'Seraphine', name: 'Séraphine' });
const KAISA = entry({ id: 'Kaisa', name: "Kai'Sa" });
const FLAT = [AHRI, SERAPHINE, KAISA];

describe('filterOptions', () => {
  it('keeps every entry for an empty or blank query', () => {
    expect(filterOptions(FLAT, '')).toEqual(FLAT);
    expect(filterOptions(FLAT, '   ')).toEqual(FLAT);
  });

  it('matches whatever the case', () => {
    expect(filterOptions(FLAT, 'AHRI')).toEqual([AHRI]);
  });

  it('matches whatever the accents, both ways', () => {
    expect(filterOptions(FLAT, 'seraphine')).toEqual([SERAPHINE]);
    expect(filterOptions(FLAT, 'SÉRA')).toEqual([SERAPHINE]);
  });

  it('keeps the order it was given, never sorting', () => {
    expect(filterOptions([KAISA, AHRI, SERAPHINE], 'a')).toEqual([KAISA, AHRI, SERAPHINE]);
  });

  it('keeps nothing when nothing matches', () => {
    expect(filterOptions(FLAT, 'zzzz')).toEqual([]);
  });

  describe('rune paths', () => {
    const DOMINATION = entry({ id: '8100', name: 'Domination', isGroup: true });
    const ELECTROCUTE = entry({ id: '8112', name: 'Électrocution', groupId: '8100' });
    const PREDATOR = entry({ id: '8124', name: 'Prédateur', groupId: '8100' });
    const PRECISION = entry({ id: '8000', name: 'Précision', isGroup: true });
    const PRESS_THE_ATTACK = entry({ id: '8005', name: 'Jeu offensif', groupId: '8000' });
    const GROUPED = [DOMINATION, ELECTROCUTE, PREDATOR, PRECISION, PRESS_THE_ATTACK];

    it('keeps the header of a matching rune and drops the other paths', () => {
      expect(filterOptions(GROUPED, 'electro')).toEqual([DOMINATION, ELECTROCUTE]);
    });

    it('keeps every rune of a matching header', () => {
      expect(filterOptions(GROUPED, 'précision')).toEqual([PRECISION, PRESS_THE_ATTACK]);
    });
  });
});

describe('picker entries', () => {
  const PRESENT = { status: 'present', url: '/cdn/blobs/a.png' } as const;

  it('searches a flat option by its name and its id, and drops absent images', () => {
    const [flash, ghost] = flatEntriesOf([
      { id: 'SummonerFlash', name: 'Flash', image: PRESENT },
      { id: 'SummonerHaste', name: 'Fantôme', image: { status: 'absent' } },
    ]);

    expect(flash).toEqual({
      id: 'SummonerFlash',
      name: 'Flash',
      image: '/cdn/blobs/a.png',
      searchText: 'flash summonerflash',
    });
    expect(ghost?.image).toBeNull();
    expect(ghost?.searchText).toBe('fantome summonerhaste');
  });

  it('lists each rune path as a header followed by its runes, slot by slot', () => {
    const rune = (id: number, name: string) => ({
      id,
      name,
      key: name,
      shortDesc: '',
      image: PRESENT,
    });
    const entries = runeEntriesOf([
      {
        id: 8100,
        key: 'Domination',
        name: 'Domination',
        image: PRESENT,
        slots: [[rune(8112, 'Électrocution')], [rune(8126, 'Coup bas')]],
      },
    ]);

    expect(entries.map(({ id, isGroup, groupId }) => ({ id, isGroup, groupId }))).toEqual([
      { id: '8100', isGroup: true, groupId: undefined },
      { id: '8112', isGroup: undefined, groupId: '8100' },
      { id: '8126', isGroup: undefined, groupId: '8100' },
    ]);
    expect(filterOptions(entries, 'coup')).toEqual([entries[0], entries[2]]);
  });
});
