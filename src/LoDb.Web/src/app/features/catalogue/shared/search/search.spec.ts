import { isSearchShortcut } from './is-search-shortcut';
import { normalizeSearchText } from './normalize-search-text';

describe('normalizeSearchText', () => {
  it('lowercases and strips accents', () => {
    expect(normalizeSearchText('Séraphine')).toBe('seraphine');
    expect(normalizeSearchText('MAÎTRE YI')).toBe('maitre yi');
  });

  it('leaves punctuation alone (apostrophes are part of champion names)', () => {
    expect(normalizeSearchText("Kai'Sa")).toBe("kai'sa");
  });
});

describe('isSearchShortcut', () => {
  const slash = (init: KeyboardEventInit = {}) =>
    new KeyboardEvent('keydown', { key: '/', ...init });

  it('answers the bare slash outside any field', () => {
    expect(isSearchShortcut(slash(), document.body)).toBe(true);
    expect(isSearchShortcut(slash(), null)).toBe(true);
  });

  it('leaves the key to a reader already typing', () => {
    expect(isSearchShortcut(slash(), document.createElement('input'))).toBe(false);
    expect(isSearchShortcut(slash(), document.createElement('textarea'))).toBe(false);
  });

  it('ignores other keys and modified slashes', () => {
    expect(isSearchShortcut(new KeyboardEvent('keydown', { key: 'a' }), null)).toBe(false);
    expect(isSearchShortcut(slash({ ctrlKey: true }), null)).toBe(false);
    expect(isSearchShortcut(slash({ metaKey: true }), null)).toBe(false);
  });
});
