import { publicProfileOf } from '../testing/public-profile-of';
import { heroOrbsOf } from './hero-orbs-of';

describe('heroOrbsOf', () => {
  it('floats the favorites set, in the order of the slots, the champion as the large one', () => {
    expect(heroOrbsOf(publicProfileOf())).toEqual([
      {
        slot: 'champion',
        image: '/cdn/blobs/ahri.png',
        initials: 'AH',
        primary: true,
        position: 1,
      },
      {
        slot: 'item',
        image: '/cdn/blobs/rabadon.png',
        initials: 'RA',
        primary: false,
        position: 2,
      },
      { slot: 'rune', image: null, initials: 'EL', primary: false, position: 3 },
    ]);
  });

  it('skips the champion when its splash is already the backdrop', () => {
    const backdrop = { kind: 'champion' as const, image: '/ahri.jpg', fallback: '/ahri-wide.jpg' };

    const orbs = heroOrbsOf(publicProfileOf({ backdrop }));

    expect(orbs.map((orb) => [orb.slot, orb.position])).toEqual([
      ['item', 1],
      ['rune', 2],
    ]);
  });

  it('floats nothing for a card without favorites the patch knows', () => {
    const card = publicProfileOf();
    const unavailable = { status: 'unavailable' as const, storedId: 'Gone', current: null };
    const favorites = { ...card.showcase.favorites, champion: unavailable, item: unavailable };
    const rune = { status: 'empty' as const, storedId: null, current: null };

    const orbs = heroOrbsOf({
      ...card,
      showcase: { ...card.showcase, favorites: { ...favorites, rune } },
    });

    expect(orbs).toEqual([]);
  });
});
