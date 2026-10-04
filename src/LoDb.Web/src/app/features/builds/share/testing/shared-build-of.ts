import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { ChampionView } from '../../../../core/api/generated/models/champion-view';
import type { ItemView } from '../../../../core/api/generated/models/item-view';
import type { PerkView } from '../../../../core/api/generated/models/perk-view';
import type { RunePageView } from '../../../../core/api/generated/models/rune-page-view';
import type { SharedBuild } from '../../../../core/api/generated/models/shared-build';
import type { StepView } from '../../../../core/api/generated/models/step-view';

function present(url: string): CatalogImage {
  return { status: 'present', url, webpUrl: null };
}

function perk(id: number, name: string, missing = false): PerkView {
  const icon: CatalogImage = missing ? { status: 'absent' } : present(`/cdn/perk-${id}.png`);
  return { id, name, icon, missing, key: null };
}

function item(id: string, name: string, missing = false): ItemView {
  const image: CatalogImage = missing ? { status: 'absent' } : present(`/cdn/item-${id}.png`);
  return { id, name, image, missing, gold: missing ? null : 1000 };
}

// Domination with a keystone the patch no longer knows, then Sorcery.
function runesOf(): RunePageView {
  return {
    primary: {
      id: 8100,
      key: 'Domination',
      name: 'Domination',
      missing: false,
      icon: present('/cdn/8100.png'),
    },
    secondary: {
      id: 8200,
      key: 'Sorcery',
      name: 'Sorcery',
      missing: false,
      icon: present('/cdn/8200.png'),
    },
    keystone: perk(9923, '9923', true),
    minors: [
      perk(8139, 'Taste of Blood'),
      perk(8138, 'Eyeball Collection'),
      perk(8135, 'Treasure Hunter'),
    ],
    secondaryPerks: [perk(8210, 'Transcendence'), perk(8237, 'Scorch')],
  };
}

const CHAMPION: ChampionView = {
  id: 'Ahri',
  name: 'Ahri',
  title: 'the Nine-Tailed Fox',
  missing: false,
  image: present('/cdn/ahri.png'),
};

// A start with an item the patch no longer knows, then the core.
function stepsOf(): StepView[] {
  return [
    {
      label: 'Start',
      note: 'Rush it.',
      gold: 1500,
      items: [item('1056', "Doran's Ring"), item('9999', '9999', true)],
    },
    { label: 'Core', note: null, gold: 3000, items: [item('3089', "Rabadon's Deathcap")] },
  ];
}

/**
 * A public French build of Ahri on an older patch, with a ghost keystone and a ghost item, as
 * `GET /api/share/{token}` answers it to a visitor.
 */
export function sharedBuildOf(overrides: Partial<SharedBuild> = {}): SharedBuild {
  return {
    id: 42,
    name: 'Mid burst',
    shareToken: '0123456789abcdef01234567',
    isPublic: true,
    gameMode: 'aram',
    gameVersion: '15.14.1',
    currentVersion: '16.19.1',
    patchMismatch: true,
    language: 'fr_FR',
    description: 'Roam after six.',
    createdAt: '2026-08-30T10:00:00Z',
    updatedAt: '2026-09-01T08:00:00Z',
    owner: { username: 'Faker', riotTagline: 'KR1', hasPublicProfile: true, isSupporter: true },
    champion: CHAMPION,
    runes: runesOf(),
    steps: stepsOf(),
    totalGold: 4500,
    vote: { score: 3, myVote: 0 },
    ...overrides,
  };
}
