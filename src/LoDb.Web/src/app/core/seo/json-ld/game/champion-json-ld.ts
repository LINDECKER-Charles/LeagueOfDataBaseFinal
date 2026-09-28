import type { ChampionDetails } from '../../../api/generated/models/champion-details';
import type { ChampionRatings } from '../../../api/generated/models/champion-ratings';
import type { ChampionStat } from '../../../api/generated/models/champion-stat';
import type { GameStat } from '../../../api/generated/models/game-stat';
import type { SeoUrls } from '../../urls/seo-urls';
import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { propertyValues } from '../core/property-values';
import { pruneJsonLd } from '../core/prune-json-ld';
import { imageOf } from './image-of';
import { videoGame } from './video-game';

// Level-1 stats worth stating, in English as structured data expects. The per-level gains
// are left out: they describe a curve, not a fact that reads well out of context.
const STATS: readonly (readonly [GameStat, string])[] = [
  ['health', 'Health'],
  ['armor', 'Armor'],
  ['magic_resist', 'Magic resist'],
  ['attack_damage', 'Attack damage'],
  ['attack_speed', 'Attack speed'],
  ['move_speed', 'Move speed'],
  ['attack_range', 'Attack range'],
];

// Riot's own 0-10 ratings: the closest thing to a play-style summary.
const RATINGS: readonly (readonly [keyof ChampionRatings, string])[] = [
  ['attack', 'Attack rating'],
  ['defense', 'Defense rating'],
  ['magic', 'Magic rating'],
  ['difficulty', 'Difficulty'],
];

/** A champion page: the champion as the game's `character`, with its roles and stats. */
export function championJsonLd(champion: ChampionDetails, urls: SeoUrls): JsonLdNode {
  const card = champion.profile;
  const ratings = card.ratings;
  const character = pruneJsonLd({
    '@type': 'Person',
    '@id': `${urls.canonical}#character`,
    name: card.name,
    alternateName: plainText(card.title),
    image: imageOf(card.image, urls),
    description: plainText(champion.blurb) ?? plainText(champion.lore),
    additionalProperty: [
      ...propertyValues('Role', card.tags),
      ...propertyValues('Resource', [card.partype]),
      ...RATINGS.flatMap(([key, label]) => propertyValues(label, [ratings?.[key]])),
      ...STATS.flatMap(([stat, label]) => propertyValues(label, [baseOf(card.stats, stat)])),
    ],
  });
  return videoGame('character', character, urls.canonical);
}

function baseOf(stats: readonly ChampionStat[], stat: GameStat): number | undefined {
  return stats.find((entry) => entry.stat === stat)?.base;
}
