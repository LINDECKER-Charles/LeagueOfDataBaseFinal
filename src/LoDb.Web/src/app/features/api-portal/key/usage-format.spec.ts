import { formatCount, formatDate, formatDay, quotaPercent } from './usage-format';

describe('usage-format', () => {
  it('groups counts by thousands with a space, whatever the locale', () => {
    expect(formatCount(12_345)).toBe('12\u00a0345');
    expect(formatCount(1_500_000)).toBe('1\u00a0500\u00a0000');
    expect(formatCount(500)).toBe('500');
  });

  it('writes the UTC day of the API as dd/mm/yyyy, whatever the time zone of the page', () => {
    expect(formatDay('2026-09-01')).toBe('01/09/2026');
    expect(formatDate('2026-09-01T23:59:59+00:00')).toBe('01/09/2026');
  });

  it('keeps a date it cannot read as it came', () => {
    expect(formatDate('soon')).toBe('soon');
  });

  it('measures the quota used, bounded to 100', () => {
    expect(quotaPercent(137, 500)).toBe(27);
    expect(quotaPercent(900, 500)).toBe(100);
    expect(quotaPercent(0, 0)).toBe(100);
  });
});
