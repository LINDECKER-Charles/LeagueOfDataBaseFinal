import { formatCount, formatDate, formatDay, quotaPercent } from './usage-format';

describe('usage-format', () => {
  it('groups counts as the locale does', () => {
    expect(formatCount('en', 12_345)).toBe('12,345');
    expect(formatCount('fr', 12_345)).toBe(new Intl.NumberFormat('fr').format(12_345));
  });

  it('writes the UTC day of the API, whatever the time zone of the page', () => {
    expect(formatDay('en', '2026-09-01')).toBe('9/1/26');
    expect(formatDate('fr', '2026-09-01T23:59:59+00:00')).toBe('01/09/2026');
  });

  it('keeps a date it cannot read as it came', () => {
    expect(formatDate('en', 'soon')).toBe('soon');
  });

  it('measures the quota used, bounded to 100', () => {
    expect(quotaPercent(137, 500)).toBe(27);
    expect(quotaPercent(900, 500)).toBe(100);
    expect(quotaPercent(0, 0)).toBe(100);
  });
});
