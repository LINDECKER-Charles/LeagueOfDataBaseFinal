import type { AdminText } from '../../../../shared/inject-admin-text';
import { scopeSummary } from './scope-summary';
import { unitShare } from './unit-share';

// The texts as their keys, with the values they interpolate.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${Object.values(params).join(' ')}` : key),
  term: (group, name) => `${group}.${name}`,
};

describe('scopeSummary', () => {
  it('names a pass over everything', () => {
    expect(scopeSummary({ priority: [] }, TEXTS)).toBe('prerender.scope.all');
  });

  it('writes the perimeter in a line, a range open at either end', () => {
    expect(
      scopeSummary(
        { oldest: '14.1.1', newest: '14.24.1', pageType: 'items', locale: 'fr', priority: ['a'] },
        TEXTS,
      ),
    ).toBe(
      'prerender.scope.range 14.1.1 14.24.1 · prerender.page_types.items · fr · ' +
        'prerender.scope.priority 1',
    );
    expect(scopeSummary({ oldest: '14.1.1', priority: [] }, TEXTS)).toBe(
      'prerender.scope.from 14.1.1',
    );
    expect(scopeSummary({ newest: '16.1.1', priority: [] }, TEXTS)).toBe(
      'prerender.scope.until 16.1.1',
    );
  });
});

describe('unitShare', () => {
  it('measures the progress by the units, nothing before they are listed', () => {
    const units = { total: 4, completed: 1, running: 1, pending: 2, failed: 0 };

    expect(unitShare(units)).toBe(0.25);
    expect(unitShare({ ...units, total: 0, completed: 0 })).toBe(0);
  });
});
