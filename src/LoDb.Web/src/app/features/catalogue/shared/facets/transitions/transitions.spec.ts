import { facetOf } from '../../testing/facet-of';
import { rangeSelectionOf } from './range-selection-of';
import { withChoiceMatchAll } from './with-choice-match-all';
import { withChoiceToggled } from './with-choice-toggled';
import { withRange } from './with-range';
import { withToggle } from './with-toggle';

const TAG = facetOf({ key: 'tag', kind: 'choice', matchAll: true });
const EDITION = facetOf({ key: 'edition', kind: 'choice', multiple: false });

describe('facet transitions', () => {
  it('toggles a value in and out, dropping the facet when empty', () => {
    const on = withChoiceToggled({}, TAG, 'Boots');
    expect(on).toEqual({ tag: { values: ['Boots'], all: false } });
    expect(withChoiceToggled(on, TAG, 'Boots')).toEqual({});
  });

  it('accumulates the values of a multiple choice in the order they are picked', () => {
    const state = withChoiceToggled(withChoiceToggled({}, TAG, 'Boots'), TAG, 'Armor');
    expect(state['tag']).toEqual({ values: ['Boots', 'Armor'], all: false });
  });

  it('replaces the value of a single-choice facet instead of accumulating', () => {
    const start = { edition: { values: ['modern'], all: false } };
    expect(withChoiceToggled(start, EDITION, 'classic')['edition']).toEqual({
      values: ['classic'],
      all: false,
    });
  });

  it('switches match mode only on an engaged choice', () => {
    expect(withChoiceMatchAll({}, 'tag', true)).toEqual({});
    const state = withChoiceMatchAll({ tag: { values: ['a'], all: false } }, 'tag', true);
    expect(state['tag']).toEqual({ values: ['a'], all: true });
  });

  it('sets and clears ranges and toggles', () => {
    const ranged = withRange({}, 'price', { min: 0, max: 10 });
    expect(ranged).toEqual({ price: { min: 0, max: 10 } });
    expect(withRange(ranged, 'price', null)).toEqual({});
    expect(withToggle({}, 'purchasable', true)).toEqual({ purchasable: true });
    expect(withToggle({ purchasable: true }, 'purchasable', false)).toEqual({});
  });

  it('never mutates the state it starts from', () => {
    const start = Object.freeze({ purchasable: true as const });
    expect(() => withToggle(start, 'purchasable', false)).not.toThrow();
    expect(start).toEqual({ purchasable: true });
  });
});

describe('rangeSelectionOf', () => {
  const bounds = { min: 0, max: 3000 };

  it('puts crossed thumbs back in order', () => {
    expect(rangeSelectionOf(900, 300, bounds)).toEqual({ min: 300, max: 900 });
  });

  it('commits nothing when the thumbs span every card', () => {
    expect(rangeSelectionOf(0, 3000, bounds)).toBeNull();
    expect(rangeSelectionOf(-5, 4000, bounds)).toBeNull();
  });
});
