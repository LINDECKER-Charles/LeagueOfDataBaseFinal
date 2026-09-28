import { pathClassOf } from './path-class-of';

describe('pathClassOf', () => {
  it('tints the page with its primary path', () => {
    expect(pathClassOf('Domination')).toBe('path-domination');
    expect(pathClassOf('Inspiration')).toBe('path-inspiration');
  });

  it.each([null, '', 'Ascension'])('falls back on Precision for %s', (key) => {
    expect(pathClassOf(key)).toBe('path-precision');
  });
});
