import type { PageContext } from '../../../../core/context/page-context';
import { catalogueHref } from './catalogue-href';

const LATEST: PageContext = { locale: 'fr', version: '16.19.1', pinned: false, language: 'fr_FR' };
const PINNED: PageContext = { ...LATEST, version: '15.14.1', pinned: true };

describe('catalogueHref', () => {
  it('leaves the latest version out of the path, and writes a pinned one', () => {
    expect(catalogueHref(LATEST, 'champions/Aatrox')).toBe('/fr/champions/Aatrox');
    expect(catalogueHref(PINNED, 'items')).toBe('/fr/15.14.1/items');
  });

  it('carries the regional variant of the current URL on, and nothing else', () => {
    const current = '/en/items?q=boots&lang=en_GB&page=2#grid';
    expect(catalogueHref({ ...LATEST, locale: 'en' }, 'items/1001-boots', current)).toBe(
      '/en/items/1001-boots?lang=en_GB',
    );
    expect(catalogueHref(LATEST, 'runes', '/fr/runes?q=x')).toBe('/fr/runes');
  });
});
