import { duration } from './duration';
import { figure } from './figure';
import { FigurePipe } from './figure-pipe';
import { stamp } from './stamp';
import { StampPipe } from './stamp-pipe';

describe('figure', () => {
  it.each([
    [0, '0'],
    [999, '999'],
    [1234567, '1 234 567'],
    [-4200, '-4 200'],
    [12.6, '13'],
  ])('writes %s as the integer %s', (value, text) => {
    expect(figure(value)).toBe(text);
  });

  it.each([
    [950, '950'],
    [1000, '1k'],
    [1250, '1.3k'],
    [3_000_000, '3M'],
    [2_450_000, '2.5M'],
  ])('writes %s compact as %s', (value, text) => {
    expect(figure(value, 'compact')).toBe(text);
  });

  it.each([
    [512, '512 B'],
    [1024, '1.00 KB'],
    [1536, '1.50 KB'],
    [5 * 1024 ** 3, '5.00 GB'],
  ])('writes %s bytes as %s', (value, text) => {
    expect(figure(value, 'bytes')).toBe(text);
  });

  it('writes cents as French euros, with a space between thousands', () => {
    expect(figure(123456, 'euros')).toBe('1 234,56 €');
    expect(figure(500, 'euros')).toBe('5,00 €');
  });

  it('writes cents as a French amount without its currency, for a foreign one', () => {
    expect(figure(123456, 'money')).toBe('1 234,56');
  });

  it('writes a share with one decimal, or rounded to a whole percent', () => {
    expect(figure(12.345, 'pct')).toBe('12.3 %');
    expect(figure(0.4567, 'share')).toBe('45.7 %');
    // As the legacy `round(1)`: a decimal that carries nothing is left out.
    expect(figure(1, 'share')).toBe('100 %');
    expect(figure(0.4567, 'percent')).toBe('46 %');
    expect(figure(1, 'percent')).toBe('100 %');
  });

  it('writes a ratio as a multiplier with two decimals', () => {
    expect(figure(2.3456, 'ratio')).toBe('2.35×');
  });

  it('reads a missing figure as zero in a template', () => {
    expect(new FigurePipe().transform(null, 'bytes')).toBe('0 B');
  });
});

describe('stamp', () => {
  it('dates an instant in UTC, whatever the offset it was written with', () => {
    expect(stamp('2026-09-27T23:30:00-02:00')).toBe('28/09/2026');
  });

  it('writes the minute and the second in UTC', () => {
    expect(stamp('2026-09-27T14:05:09Z', 'minute')).toBe('27/09/2026 14:05');
    expect(stamp('2026-09-27T14:05:09Z', 'second')).toBe('27/09/2026 14:05:09');
  });

  it('reads a missing or broken instant as a dash', () => {
    expect(stamp(null)).toBe('—');
    expect(new StampPipe().transform('not a date')).toBe('—');
  });
});

describe('duration', () => {
  it.each([
    [-3, '0 s'],
    [42.9, '42 s'],
    [60, '1 min'],
    [4 * 3600 + 12 * 60 + 30, '4 h 12 min'],
    [3 * 86_400 + 4 * 3600 + 59 * 60, '3 j 4 h'],
  ])('writes %s seconds as %s', (seconds, text) => {
    expect(duration(seconds)).toBe(text);
  });
});
