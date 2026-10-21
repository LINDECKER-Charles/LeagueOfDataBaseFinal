import type { AdminText } from '../../../../../shared/inject-admin-text';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { cellTooltip } from './cell-tooltip';

// The texts as their last key segment, with the values they interpolate.
const TEXTS: AdminText = {
  text: (key, params) => {
    const name = key.split('.').at(-1);
    return params ? `${name}(${Object.values(params).join('|')})` : `${name}`;
  },
  term: (_group, name) => name,
};

const RUNNING = prerenderProgress().running[0];

describe('cellTooltip', () => {
  it('says the pages, the volume and the time of a running unit known in detail', () => {
    expect(
      cellTooltip(
        { mode: 'pass', version: '16.18.1', locale: 'fr', code: 'r', unit: RUNNING },
        TEXTS,
      ),
    ).toBe('cell(16.18.1|fr|français|running)\ncell_pages(312|1140|12.05 MB) · cell_since(4 min)');
  });

  it('says where a stopped unit resumes', () => {
    expect(
      cellTooltip(
        { mode: 'pass', version: '16.18.1', locale: 'fr', code: 'z', unit: RUNNING },
        TEXTS,
      ),
    ).toBe('cell(16.18.1|fr|français|stopped)\ncell_stopped(312|1 140)');
  });

  it('says the state alone of a cell known alone, and the rank of one to come', () => {
    expect(cellTooltip({ mode: 'pass', version: '15.2.1', locale: 'th', code: 'c' }, TEXTS)).toBe(
      'cell(15.2.1|th|thaï|complete)',
    );
    expect(
      cellTooltip({ mode: 'pass', version: '14.1.1', locale: 'ar', code: '.', rank: 1_234 }, TEXTS),
    ).toBe('cell(14.1.1|ar|arabe|pending)\ncell_rank(1 234)');
  });

  it('says the types a cell of a coverage holds', () => {
    const cell = { mode: 'coverage', version: '16.18.1', locale: 'fr', code: 'p' } as const;

    expect(cellTooltip({ ...cell, held: '6' }, TEXTS)).toBe(
      'cell(16.18.1|fr|français|partial) · cell_held(items, runes)',
    );
    expect(cellTooltip({ ...cell, code: '-', held: '0' }, TEXTS)).toBe(
      'cell(16.18.1|fr|français|absent) · cell_held_none',
    );
  });
});
