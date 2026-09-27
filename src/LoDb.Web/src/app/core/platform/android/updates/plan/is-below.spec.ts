import { isBelow } from './is-below';

describe('isBelow', () => {
  it.each([
    ['1.2.3', '1.2.4', true],
    ['1.2.3', '1.3.0', true],
    ['1.9.9', '2.0.0', true],
    ['1.2.3', '1.2.3', false],
    ['1.2.10', '1.2.9', false],
    ['2.0.0', '1.99.99', false],
  ])('compares %s with %s by number, part by part', (version, floor, below) => {
    expect(isBelow(version, floor)).toBe(below);
  });

  it('puts a pre-release before its release, as the API does for its 426', () => {
    expect(isBelow('1.4.0-beta.3', '1.4.0')).toBe(true);
    expect(isBelow('1.4.0-beta.3', '1.3.9')).toBe(false);
    expect(isBelow('1.4.0-beta.3', '1.4.1')).toBe(true);
  });

  it.each([
    ['', '1.0.0'],
    ['1.0', '1.0.0'],
    ['v1.0.0', '1.0.0'],
    ['1.0.0-', '1.0.0'],
    ['1.0.0-beta..1', '1.0.0'],
    ['1.0.0', '1.0.0-beta.1'],
    ['1.0.0', 'latest'],
    [' 1.0.0', '1.0.0'],
  ])('knows nothing of a malformed version (%j against %j)', (version, floor) => {
    expect(isBelow(version, floor)).toBeNull();
  });
});
