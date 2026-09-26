import { facetOf } from '../testing/facet-of';
import { describeSelection } from './describe-selection';
import { isPlainClick } from './is-plain-click';

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
