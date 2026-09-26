import type { CatalogImage } from '../../../core/api/generated/models/catalog-image';
import type { FavoriteView } from '../../../core/api/generated/models/favorite-view';
import type { ProfileBuildCard } from '../../../core/api/generated/models/profile-build-card';
import type { ProfileShowcase } from '../../../core/api/generated/models/profile-showcase';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';

function present(url: string): CatalogImage {
  return { status: 'present', url, webpUrl: null };
}

function favorite(id: string, name: string, image: CatalogImage): FavoriteView {
  return { status: 'resolved', storedId: id, current: { id, name, image } };
}

function showcaseOf(): ProfileShowcase {
  return {
    version: '16.19.1',
    language: 'en_US',
    isCatalogAvailable: true,
    skin: {
      id: 'Ahri_7',
      championId: 'Ahri',
      number: 7,
      name: 'Arcade Ahri',
      banner: '/cdn/ahri-7-centered.jpg',
      splash: '/cdn/ahri-7.jpg',
    },
    favorites: {
      champion: favorite('Ahri', 'Ahri', present('/cdn/blobs/ahri.png')),
      item: favorite('3089', "Rabadon's Deathcap", present('/cdn/blobs/rabadon.png')),
      rune: favorite('8112', 'Electrocute', { status: 'absent' }),
      summoner: { status: 'empty', storedId: null, current: null },
    },
  };
}

function buildsOf(): ProfileBuildCard[] {
  const updatedAt = '2026-09-01T08:00:00Z';
  return [
    {
      shareToken: 'k3y-1',
      name: 'Mid burst',
      championId: 'Ahri',
      championName: 'Ahri',
      championImage: present('/cdn/blobs/ahri.png'),
      gameVersion: '16.19.1',
      updatedAt,
    },
    {
      shareToken: 'k3y-2',
      name: 'Old times',
      championId: 'Zeri',
      championName: null,
      championImage: { status: 'absent' },
      gameVersion: '11.1.1',
      updatedAt,
    },
  ];
}

/**
 * The card of the specs: Faker#KR1, a supporter since March 2024, the Arcade Ahri skin as
 * backdrop, three favorites (the rune without an image, no summoner spell) and two builds,
 * the second on a patch that lacks its champion.
 */
export function publicProfileOf(overrides: Partial<PublicProfile> = {}): PublicProfile {
  return {
    username: 'Faker',
    riotTagline: 'KR1',
    isPublic: true,
    isSupporter: true,
    memberSince: '2024-03-05T22:30:00Z',
    backdrop: { kind: 'skin', image: '/cdn/ahri-7-centered.jpg', fallback: '/cdn/ahri-7.jpg' },
    showcase: showcaseOf(),
    builds: buildsOf(),
    ...overrides,
  };
}
