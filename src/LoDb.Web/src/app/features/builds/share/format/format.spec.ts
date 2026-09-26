import { pathClassOf } from './path-class-of';
import { shortDate } from './short-date';

describe('shortDate', () => {
  it('writes the day, the month and the year of the API’s UTC date', () => {
    expect(shortDate('2026-03-05T23:30:00Z')).toBe('05/03/2026');
    expect(shortDate('2026-12-31T00:00:00Z')).toBe('31/12/2026');
  });
});

describe('pathClassOf', () => {
  it('tints the page with its primary path', () => {
    expect(pathClassOf('Domination')).toBe('path-domination');
    expect(pathClassOf('Inspiration')).toBe('path-inspiration');
  });

  it.each([null, '', 'Ascension'])('falls back on Precision for %s', (key) => {
    expect(pathClassOf(key)).toBe('path-precision');
  });
});
