import { parseEuroAmount } from './parse-euro-amount';

describe('parseEuroAmount', () => {
  it.each([
    ['7', 700],
    ['7.5', 750],
    ['7,50', 750],
    ['0.99', 99],
    ['500', 50_000],
    ['999,99', 99_999],
    [' 12 ', 1_200],
  ])('reads %j as %i cents', (text, cents) => {
    expect(parseEuroAmount(text)).toBe(cents);
  });

  it.each(['', ' ', 'abc', '1000', '7.505', '7.', '.50', '-5', '5e2', '7 50', '1,000.00'])(
    'refuses %j',
    (text) => {
      expect(parseEuroAmount(text)).toBeNull();
    },
  );
});
