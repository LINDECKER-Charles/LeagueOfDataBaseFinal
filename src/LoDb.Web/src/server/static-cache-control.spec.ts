import { staticCacheControl } from './static-cache-control';

describe('staticCacheControl', () => {
  const immutable = 'public, max-age=31536000, immutable';
  const revalidated = 'public, max-age=0, must-revalidate';

  it.each([
    'main-2ZNNMVSG.js',
    'chunk-C1xKWD6m.js',
    'chunk-a_b-C1x9.js',
    'styles-5INURTSO.css',
    'media/beaufort-PL7H3MLA.woff2',
  ])('caches the hashed %s for a year', (file) => {
    expect(staticCacheControl(file)).toBe(immutable);
  });

  it.each([
    'i18n/fr.json',
    'i18n/zh-hans.json',
    'favicon.ico',
    'index.csr.html',
    'sw.js',
    'service-worker.js',
    'scripts/vendor-12345678.js',
    'icons/maskable-12345678.png',
  ])('revalidates %s on every use', (file) => {
    expect(staticCacheControl(file)).toBe(revalidated);
  });

  it('lets the HTTP transfer cache embed a catalogue fetched during SSR', () => {
    // Angular drops any response whose Cache-Control names one of these directives.
    expect(staticCacheControl('i18n/en.json')).not.toMatch(/no-cache|no-store|private/);
  });
});
