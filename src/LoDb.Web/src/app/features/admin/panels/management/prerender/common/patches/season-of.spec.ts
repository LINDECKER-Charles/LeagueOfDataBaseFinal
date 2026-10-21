import { seasonOf } from './season-of';

describe('seasonOf', () => {
  it('dates a season by its first segment, from 3.x in 2013', () => {
    expect(seasonOf('16.18.1')).toEqual({ key: '16', major: 16, year: 2026 });
    expect(seasonOf('3.15.1')).toEqual({ key: '3', major: 3, year: 2013 });
  });

  it('leaves the year out of a segment no season fits', () => {
    expect(seasonOf('0.151.2')).toEqual({ key: '0', major: 0, year: null });
    expect(seasonOf('100.1')).toEqual({ key: '100', major: 100, year: null });
  });
});
