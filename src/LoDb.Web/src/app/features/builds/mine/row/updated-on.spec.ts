import { updatedOn } from './updated-on';

describe('updatedOn', () => {
  it('prints the day in the locale of the page', () => {
    expect(updatedOn('2026-09-26T08:30:00Z', 'fr')).toBe('26/09/2026');
    expect(updatedOn('2026-09-26T08:30:00Z', 'en')).toBe('09/26/2026');
  });

  it('reads the stamp in UTC, so the day never shifts with the reader', () => {
    expect(updatedOn('2026-09-26T23:59:00Z', 'fr')).toBe('26/09/2026');
  });

  it('keeps a stamp it cannot read as it is', () => {
    expect(updatedOn('not a date', 'fr')).toBe('not a date');
  });
});
