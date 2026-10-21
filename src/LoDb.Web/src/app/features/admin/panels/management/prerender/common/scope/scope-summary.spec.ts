import type { PrerenderScopeView } from '../../../../../../../core/api/generated/models/prerender-scope-view';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { scopeSummary } from './scope-summary';

// The texts as their keys, short, with the values they interpolate.
const TEXTS: AdminText = {
  text: (key, params) => {
    const name = key.replace('prerender.common.scope.', '');
    return params ? `${name}(${Object.values(params).join(' ')})` : name;
  },
  term: (_group, name) => `<${name}>`,
};

const WHOLE: PrerenderScopeView = { priority: [], skipComplete: false };

function summary(changes: Partial<PrerenderScopeView>): string {
  return scopeSummary({ ...WHOLE, ...changes }, TEXTS);
}

describe('scopeSummary', () => {
  it('names a pass over everything in a word', () => {
    expect(summary({})).toBe('all');
  });

  it('counts the listed versions and gives their bounds, then the locales and the types', () => {
    expect(summary({ versionCount: 212, newestListed: '16.19.1', oldestListed: '14.1.1' })).toBe(
      'listed(212 16.19.1 14.1.1) · locales_all · types_all',
    );
  });

  it('names a single version by itself', () => {
    expect(
      summary({
        versionCount: 1,
        newestListed: '16.19.1',
        oldestListed: '16.19.1',
        locales: ['fr'],
      }),
    ).toBe('16.19.1 · fr · types_all');
  });

  it('writes the range of a pass launched before the lists, open at either end', () => {
    expect(summary({ oldest: '14.1.1', newest: '14.24.1' })).toBe(
      'range(14.1.1 14.24.1) · locales_all · types_all',
    );
    expect(summary({ oldest: '14.1.1' })).toBe('from(14.1.1) · locales_all · types_all');
    expect(summary({ newest: '16.1.1' })).toBe('until(16.1.1) · locales_all · types_all');
  });

  it('reads the single locale and type the API reads from a scope stored before the lists', () => {
    expect(
      summary({ oldest: '16.19.1', newest: '16.19.1', locales: ['fr'], pageTypes: ['items'] }),
    ).toBe('16.19.1 · fr · <items>');
  });

  it('counts the locales past three, and every version of a scope restricted otherwise', () => {
    expect(summary({ locales: ['de', 'en', 'es', 'fr'], pageTypes: ['items', 'runes'] })).toBe(
      'all · locales(4) · <items>, <runes>',
    );
  });

  it('counts the priorities and says when the units held were left out', () => {
    expect(summary({ priority: ['16.18.1', '16.12.1', '15.2.1'], skipComplete: true })).toBe(
      'all · priority(3) · skip',
    );
  });
});
