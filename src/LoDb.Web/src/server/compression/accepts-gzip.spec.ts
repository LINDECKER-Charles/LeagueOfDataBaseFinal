import { acceptsGzip } from './accepts-gzip';

describe('acceptsGzip', () => {
  it.each([
    ['nginx', 'gzip'],
    ['a browser', 'gzip, deflate, br, zstd'],
    ['a weighted list', 'br;q=1.0, gzip;q=0.8, *;q=0.1'],
    ['the legacy name', 'x-gzip'],
    ['any coding', '*'],
    ['another case and spacing', ' GZip ; Q=0.5 '],
  ])('admits gzip for %s', (_case, header) => {
    expect(acceptsGzip(header)).toBe(true);
  });

  it.each([
    ['no header', undefined],
    ['an empty header', ''],
    ['the identity only', 'identity'],
    ['other codings only', 'br, zstd'],
    ['gzip refused', 'gzip;q=0'],
    ['gzip refused before any other coding', 'gzip;q=0, *'],
    ['any coding refused', '*;q=0'],
    ['an unreadable weight', 'gzip;q=high'],
  ])('refuses gzip for %s', (_case, header) => {
    expect(acceptsGzip(header)).toBe(false);
  });
});
