import { pageKeyOf } from './page-key-of';

describe('pageKeyOf', () => {
  it('tells pages apart by path, language and version', () => {
    expect(pageKeyOf('/fr/items')).not.toBe(pageKeyOf('/fr/runes'));
    expect(pageKeyOf('/fr/items')).not.toBe(pageKeyOf('/fr/items?lang=fr_FR'));
    expect(pageKeyOf('/fr/items')).not.toBe(pageKeyOf('/fr/items?version=16.1.1'));
  });

  it('ignores the other parameters and their order', () => {
    expect(pageKeyOf('/fr/items?q=boots&lang=fr_FR&page=2')).toBe(
      pageKeyOf('/fr/items?lang=fr_FR'),
    );
  });
});
