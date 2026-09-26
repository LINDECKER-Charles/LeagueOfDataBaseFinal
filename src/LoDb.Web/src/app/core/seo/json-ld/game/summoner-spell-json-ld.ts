import type { SummonerDetails } from '../../../api/generated/models/summoner-details';
import type { SeoUrls } from '../../urls/seo-urls';
import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { propertyValues } from '../core/property-values';
import { pruneJsonLd } from '../core/prune-json-ld';
import { imageOf } from './image-of';
import { videoGame } from './video-game';

/**
 * A summoner spell page: the spell as a `gameItem`, with the three values that tell spells
 * apart. The rank-1 values are the facts; a whole-map range is a sentinel, not a distance.
 */
export function summonerSpellJsonLd(spell: SummonerDetails, urls: SeoUrls): JsonLdNode {
  const card = spell.profile;
  const range = spell.globalRange ? undefined : spell.range[0];
  const entity = pruneJsonLd({
    '@type': 'Thing',
    '@id': `${urls.canonical}#spell`,
    name: card.name,
    image: imageOf(card.image, urls),
    description: plainText(card.description),
    additionalProperty: [
      ...propertyValues('Cooldown', [card.cooldown[0]], 'seconds'),
      ...propertyValues('Range', [range], 'units'),
      ...propertyValues('Required level', [card.summonerLevel]),
    ],
  });
  return videoGame('gameItem', entity, urls.canonical);
}
