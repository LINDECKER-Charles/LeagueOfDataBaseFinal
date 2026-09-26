import { reportNoticesOf } from './report-notices-of';

describe('reportNoticesOf', () => {
  const clean = { championMissing: false, runesReset: false, droppedItems: [] };

  it('only says where a clean import landed', () => {
    expect(reportNoticesOf(clean, '16.19.1')).toEqual([
      { key: 'build.import.done', params: { version: '16.19.1' } },
    ]);
  });

  it('tells the missing champion and the reset runes, in that order', () => {
    const keys = reportNoticesOf({ ...clean, championMissing: true, runesReset: true }, '1').map(
      (notice) => notice.key,
    );

    expect(keys).toEqual([
      'build.import.done',
      'build.import.champion_missing',
      'build.import.runes_reset',
    ]);
  });

  it('names each dropped item once, in step order', () => {
    const droppedItems = [
      { id: '3031', name: 'Infinity Edge', step: 0 },
      { id: '1001', name: 'Boots', step: 1 },
      { id: '3031', name: 'Infinity Edge', step: 2 },
    ];

    expect(reportNoticesOf({ ...clean, droppedItems }, '1').at(-1)).toEqual({
      key: 'build.import.items_dropped',
      params: { items: 'Infinity Edge, Boots' },
    });
  });
});
