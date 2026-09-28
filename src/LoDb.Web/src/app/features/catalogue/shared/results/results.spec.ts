import { facetOf } from '../testing/facet-of';
import { describeSelection } from './describe-selection';
import { isPlainClick } from './is-plain-click';
import { splitAroundCount } from './split-around-count';

describe('describeSelection', () => {
  const role = facetOf({
    key: 'role',
    kind: 'choice',
    label: 'Role',
    options: [
      { value: 'Mage', label: 'Mage' },
      { value: 'Tank', label: 'Tank' },
    ],
  });

  it('names a choice by its labels, joined by how they combine', () => {
    expect(describeSelection(role, { values: ['Mage', 'Tank'], all: false })).toBe(
      'Role: Mage, Tank',
    );
    expect(describeSelection(role, { values: ['Mage', 'Unknown'], all: true })).toBe(
      'Role: Mage + Unknown',
    );
  });

  it('names a range by its bounds and unit, and a flag by its label', () => {
    const price = facetOf({ key: 'price', kind: 'range', label: 'Price', unit: 'g' });
    expect(describeSelection(price, { min: 0, max: 3000 })).toBe('Price 0–3000 g');
    const shop = facetOf({ key: 'shop', kind: 'toggle', label: 'In shop' });
    expect(describeSelection(shop, true)).toBe('In shop');
  });
});

describe('isPlainClick', () => {
  it('leaves modified and middle clicks to the browser', () => {
    expect(isPlainClick(new MouseEvent('click', { button: 0 }))).toBe(true);
    expect(isPlainClick(new MouseEvent('click', { button: 1 }))).toBe(false);
    expect(isPlainClick(new MouseEvent('click', { ctrlKey: true }))).toBe(false);
    expect(isPlainClick(new MouseEvent('click', { metaKey: true }))).toBe(false);
  });
});

describe('splitAroundCount', () => {
  it('cuts a count around its figure, the words kept apart', () => {
    expect(splitAroundCount('173 results', 173)).toEqual({
      before: '',
      figure: '173',
      after: 'results',
    });
    expect(splitAroundCount('Résultats : 62', 62)).toEqual({
      before: 'Résultats :',
      figure: '62',
      after: '',
    });
  });

  it('finds a figure grouped the way the locale writes it', () => {
    expect(splitAroundCount('1 234 résultats', 1234).figure).toBe('1 234');
    expect(splitAroundCount('Top 5: 1,234 results', 1234).before).toBe('Top 5:');
  });

  it('leaves a message without the figure whole', () => {
    expect(splitAroundCount('٦٢ نتيجة', 62)).toEqual({
      before: '٦٢ نتيجة',
      figure: null,
      after: '',
    });
  });
});
