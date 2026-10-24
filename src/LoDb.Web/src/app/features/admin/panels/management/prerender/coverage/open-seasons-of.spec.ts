import type { CoverageGroup } from './coverage-groups';
import { openSeasonsOf } from './open-seasons-of';

// A season of `rows` patches, `missing` of them with a gap.
function season(key: string, rows: number, missing: number): CoverageGroup {
  const row = { version: `${key}.1.1` } as CoverageGroup['rows'][number];
  return {
    key,
    major: Number(key),
    year: null,
    rows: Array.from({ length: rows }, () => row),
    complete: rows - missing,
    missing,
  };
}

describe('openSeasonsOf', () => {
  it('opens every season with a gap while their rows hold within the limit', () => {
    const groups = [season('16', 20, 2), season('15', 24, 0), season('14', 24, 1)];

    expect([...openSeasonsOf(groups, 60)]).toEqual(['16', '14']);
  });

  it('stops at the season that would pass the limit, the newest ones open', () => {
    const groups = [season('16', 20, 20), season('15', 24, 24), season('14', 24, 24)];

    expect([...openSeasonsOf(groups, 60)]).toEqual(['16', '15']);
  });

  it('opens the newest season with a gap however long, and nothing without one', () => {
    expect([...openSeasonsOf([season('16', 80, 80), season('15', 5, 5)], 60)]).toEqual(['16']);
    expect(openSeasonsOf([season('16', 20, 0)], 60).size).toBe(0);
  });
});
