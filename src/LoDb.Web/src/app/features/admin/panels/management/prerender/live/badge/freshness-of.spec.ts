import { freshnessOf } from './freshness-of';

const NOW = 1_000_000;

function at(ageMs: number, every = 5_000, isHidden = false) {
  return freshnessOf({ lastSuccessAt: NOW - ageMs, every, isHidden }, NOW);
}

describe('freshnessOf', () => {
  it('reads live within two cadences and the time of an answer', () => {
    expect(at(0)).toBe('live');
    expect(at(12_000)).toBe('live');
  });

  it('reads late past that, within a minute or four cadences', () => {
    expect(at(12_001)).toBe('late');
    expect(at(60_000)).toBe('late');
    expect(at(240_000, 60_000)).toBe('late');
  });

  it('reads lost past a minute or four cadences', () => {
    expect(at(60_001)).toBe('lost');
    expect(at(240_001, 60_000)).toBe('lost');
  });

  it('reads hidden while the tab is, whatever the age', () => {
    expect(at(0, 5_000, true)).toBe('hidden');
    expect(at(600_000, 5_000, true)).toBe('hidden');
  });

  it('says nothing before the first answer', () => {
    expect(freshnessOf({ lastSuccessAt: null, every: 5_000, isHidden: false }, NOW)).toBeNull();
  });
});
