import { decimalBytes } from './decimal-bytes';

describe('decimalBytes', () => {
  it('steps by a thousand, the unit the prices are quoted in', () => {
    expect(decimalBytes(9_264_000_000)).toBe('9.26 GB');
    expect(decimalBytes(24_000_000)).toBe('24.00 MB');
    expect(decimalBytes(1_500)).toBe('1.50 kB');
  });

  it('writes a few bytes whole, and nothing as zero', () => {
    expect(decimalBytes(999)).toBe('999 B');
    expect(decimalBytes(0)).toBe('0 B');
  });
});
