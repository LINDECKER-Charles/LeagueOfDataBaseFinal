import { isHeld } from './is-held';

describe('isHeld', () => {
  it('holds every type behind an f, whatever is asked', () => {
    expect(isHeld('f', null)).toBe(true);
    expect(isHeld('f', ['items', 'runes'])).toBe(true);
  });

  it('holds the types a partial mask includes, and no other', () => {
    // 2 is the items alone; d is champions, runes and summoners.
    expect(isHeld('2', ['items'])).toBe(true);
    expect(isHeld('2', ['items', 'runes'])).toBe(false);
    expect(isHeld('d', ['champions', 'summoners'])).toBe(true);
    expect(isHeld('d', ['items'])).toBe(false);
  });

  it('asks for all four types when none is named', () => {
    expect(isHeld('7', null)).toBe(false);
    expect(isHeld('0', null)).toBe(false);
  });

  it('holds nothing for a mask or a type it cannot read', () => {
    expect(isHeld('-', ['items'])).toBe(false);
    expect(isHeld('f', ['skins'])).toBe(false);
  });
});
