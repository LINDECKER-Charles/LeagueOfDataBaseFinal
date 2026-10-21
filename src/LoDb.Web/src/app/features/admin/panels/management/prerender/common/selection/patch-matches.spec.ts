import { patchMatches } from './patch-matches';

describe('patchMatches', () => {
  it('takes a version equal to the query', () => {
    expect(patchMatches('16.18.1', '16.18.1')).toBe(true);
    expect(patchMatches('16.18.1', '16.18.2')).toBe(false);
  });

  it('takes a whole season from its first segment', () => {
    expect(patchMatches('15.24.1', '15')).toBe(true);
    expect(patchMatches('16.1.1', '15')).toBe(false);
  });

  it('reads a token as whole segments, unless it ends with a star', () => {
    expect(patchMatches('16.1.1', '16.1')).toBe(true);
    expect(patchMatches('16.10.1', '16.1')).toBe(false);
    expect(patchMatches('16.10.1', '16.1*')).toBe(true);
  });

  it('takes a version that matches any of several tokens', () => {
    expect(patchMatches('14.3.1', '16.1, 14')).toBe(true);
    expect(patchMatches('15.3.1', '16.1  14')).toBe(false);
  });

  it('lets everything through an empty query', () => {
    expect(patchMatches('3.15.1', '')).toBe(true);
    expect(patchMatches('3.15.1', ' , ')).toBe(true);
  });
});
