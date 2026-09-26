import type { CatalogImage } from '../../../../../core/api/generated/models/catalog-image';
import type { RuneEntry } from '../../../../../core/api/generated/models/rune-entry';
import type { RuneTreeDetails } from '../../../../../core/api/generated/models/rune-tree-details';
import { constellationOf } from './constellation-of';

const IMAGE: CatalogImage = { status: 'present', url: '/cdn/blobs/a.png' };

function rune(id: number, key: string, texts: Partial<RuneEntry> = {}): RuneEntry {
  return {
    id,
    key,
    name: key,
    image: IMAGE,
    shortDesc: `${key} short`,
    longDesc: `${key} long`,
    ...texts,
  };
}

function treeOf(slots: RuneTreeDetails['slots']): RuneTreeDetails {
  return {
    canonicalPath: 'runes/precision',
    language: 'en_US',
    version: '16.19.1',
    profile: {
      canonicalPath: 'runes/precision',
      id: 8000,
      image: IMAGE,
      key: 'Precision',
      name: 'Precision',
    },
    slots,
  };
}

describe('constellationOf', () => {
  it('strings the keystones on top, then the minor rows by their number', () => {
    const constellation = constellationOf(
      treeOf([
        { slot: 'row2', runes: [rune(9104, 'LegendAlacrity')] },
        { slot: 'keystone', runes: [rune(8005, 'PressTheAttack'), rune(8008, 'LethalTempo')] },
        { slot: 'row1', runes: [rune(9101, 'Overheal')] },
        { slot: 'row3', runes: [rune(8014, 'CoupDeGrace')] },
      ]),
    );

    expect(constellation?.keystones.map((star) => star.key)).toEqual([
      'PressTheAttack',
      'LethalTempo',
    ]);
    expect(constellation?.rows.map((row) => [row.slot, row.number])).toEqual([
      ['row1', 1],
      ['row2', 2],
      ['row3', 3],
    ]);
    expect(constellation?.rows[0]?.runes[0]?.anchor).toBe('rune-Overheal');
  });

  it('reads a keystone in full, a minor rune by its summary and its long text on demand', () => {
    const constellation = constellationOf(
      treeOf([
        { slot: 'keystone', runes: [rune(8005, 'PressTheAttack')] },
        {
          slot: 'row1',
          runes: [
            rune(9101, 'Overheal'),
            rune(9111, 'Triumph', { longDesc: 'Triumph short' }),
            rune(8009, 'PresenceOfMind', { shortDesc: '' }),
          ],
        },
      ]),
    );

    expect(constellation?.keystones[0]).toMatchObject({
      summary: 'PressTheAttack long',
      details: null,
    });
    expect(constellation?.rows[0]?.runes.map(({ summary, details }) => [summary, details])).toEqual(
      [
        ['Overheal short', 'Overheal long'],
        ['Triumph short', null],
        ['PresenceOfMind long', null],
      ],
    );
  });

  it('has no constellation for a path without runes, and skips an empty row', () => {
    expect(constellationOf(treeOf([]))).toBeNull();
    expect(constellationOf(treeOf([{ slot: 'keystone', runes: [] }]))).toBeNull();
    const rows = constellationOf(
      treeOf([
        { slot: 'row1', runes: [] },
        { slot: 'row2', runes: [rune(1, 'A')] },
      ]),
    )?.rows;
    expect(rows?.map((row) => row.slot)).toEqual(['row2']);
  });
});
