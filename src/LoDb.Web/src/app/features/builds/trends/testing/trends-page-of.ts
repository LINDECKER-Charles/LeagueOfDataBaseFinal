import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { TrendRow } from '../../../../core/api/generated/models/trend-row';
import type { TrendsPage } from '../../../../core/api/generated/models/trends-page';

function present(url: string): CatalogImage {
  return { status: 'present', url, webpUrl: null };
}

function rowOf(id: number, name: string, score: number): TrendRow {
  return {
    id,
    name,
    score,
    myVote: 0,
    shareToken: `${id}`.padStart(24, 'a'),
    gameMode: 'sr',
    gameVersion: '16.19.1',
    language: 'en_US',
    createdAt: '2026-09-01T08:00:00Z',
    owner: { username: 'Faker', riotTagline: 'KR1', hasPublicProfile: false, isSupporter: true },
    champion: { id: 'Ahri', name: 'Ahri', missing: false, image: present('/cdn/ahri.png') },
    keystone: { id: 8112, name: 'Electrocute', missing: false, icon: present('/cdn/8112.png') },
    items: [
      { id: '1056', name: "Doran's Ring", missing: false, image: present('/cdn/1056.png') },
      { id: '9999', name: '9999', missing: true, image: { status: 'absent' } },
    ],
  };
}

/**
 * The first of two pages of trends, as `GET /api/trends` answers a visitor: two builds in vote
 * order, the filters applied and the facets to change them.
 */
export function trendsPageOf(overrides: Partial<TrendsPage> = {}): TrendsPage {
  return {
    rows: [rowOf(1, 'Mid burst', 5), rowOf(2, 'Old times', -1)],
    total: 22,
    page: 1,
    pages: 2,
    perPage: 20,
    filters: { champion: null, mode: null, language: null },
    championOptions: [
      { id: 'Ahri', key: '103', name: 'Ahri', image: present('/cdn/ahri.png') },
      { id: 'MonkeyKing', key: '62', name: 'Wukong', image: present('/cdn/wukong.png') },
    ],
    languageOptions: ['en_US', 'fr_FR'],
    ...overrides,
  };
}
