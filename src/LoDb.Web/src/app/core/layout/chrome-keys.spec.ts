import CHROME_KEYS from '../../../../i18n/chrome-keys.json';
import ROOT from '../../../../public/i18n/en.json';
import FOOTER from './footer/footer.html' with { loader: 'text' };
import HEADER from './header/header.html' with { loader: 'text' };
import { SITE_LINKS } from './footer/site-links';

// A key literal a template translates: `'scope.group.name' | transloco`.
const TRANSLATED_KEY = /'([a-z][\w-]*(?:\.[\w-]+)+)'\s*\|\s*transloco/g;

function lent(): Set<string> {
  return new Set(
    Object.entries(CHROME_KEYS).flatMap(([scope, paths]) =>
      paths.map((path) => `${scope}.${path}`),
    ),
  );
}

// The chrome renders on every page outside any scope: a key the root catalogue does not hold
// is one the build must lend to it, or the page shows it raw.
describe('the labels of the chrome', () => {
  const keys = [
    ...SITE_LINKS.map((entry) => entry.label),
    ...[...HEADER.matchAll(TRANSLATED_KEY), ...FOOTER.matchAll(TRANSLATED_KEY)].map(
      (match) => match[1],
    ),
  ];

  it('are read in templates and links', () => {
    expect(keys).toContain('api.nav.developers');
  });

  it('come from the root catalogue, or are lent to it by the build', () => {
    const outsideRoot = keys.filter((key) => !Object.hasOwn(ROOT, key.split('.')[0]));

    expect(outsideRoot.length).toBeGreaterThan(0);
    expect(outsideRoot.filter((key) => !lent().has(key))).toEqual([]);
  });
});
