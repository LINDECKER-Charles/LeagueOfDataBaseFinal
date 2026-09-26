import type { JsonLdNode } from '../core/json-ld-node';
import { SCHEMA_ORG } from '../core/schema-org';

/** The game every entity belongs to: a product name, never translated. */
const GAME_NAME = 'League of Legends';
/** Rights holder of the game data: states provenance to consumers. */
const GAME_PUBLISHER = 'Riot Games';
const GAME_PLATFORM = 'PC';

/**
 * The VideoGame node a detail page states, its entity nested as `character` (a champion) or
 * `gameItem` (an item, a rune path, a summoner spell). Never a Product nor an Offer: gold is
 * no currency and nothing here can be bought.
 */
export function videoGame(
  role: 'character' | 'gameItem',
  entity: JsonLdNode,
  url: string,
): JsonLdNode {
  return {
    '@context': SCHEMA_ORG,
    '@type': 'VideoGame',
    '@id': `${url}#game`,
    name: GAME_NAME,
    url,
    gamePlatform: GAME_PLATFORM,
    publisher: { '@type': 'Organization', name: GAME_PUBLISHER },
    [role]: entity,
  };
}
