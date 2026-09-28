import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import type { GameStat } from '../../../../core/api/generated/models/game-stat';
import type { CardValues } from '../../shared/facets/model/card-values';
import type { FacetValue } from '../../shared/facets/model/facet-value';
import type { CatalogueCardAdapter } from '../../shared/state/catalogue-card-adapter';
import { CHAMPION_BASE_STATS } from './champion-base-stats';

type Values = Record<string, FacetValue>;

function roundedStat(card: ChampionCard, stat: GameStat): number | undefined {
  const base = card.stats.find((entry) => entry.stat === stat)?.base;
  return base === undefined ? undefined : Math.round(base * 1000) / 1000;
}

function ratingValues(card: ChampionCard): Values {
  const ratings = card.ratings;
  return ratings ? { ...ratings } : {};
}

function statValues(card: ChampionCard): Values {
  const values: Values = {};
  for (const { key, stat } of CHAMPION_BASE_STATS) {
    const value = roundedStat(card, stat);
    if (value !== undefined) {
      values[key] = value;
    }
  }
  return values;
}

/**
 * How the list's filter reads a champion: its name and id for the search (Wukong answers to
 * MonkeyKing), its roles, resource and range as choices, Riot's ratings and its level-1
 * stats as ranges. The resource is the API's language-independent token, so a shared URL
 * means the same in every locale.
 */
export const CHAMPION_CARD_ADAPTER: CatalogueCardAdapter<ChampionCard> = {
  searchTextOf: (card) => `${card.name} ${card.id}`,
  valuesOf: (card): CardValues => ({
    role: card.tags,
    resource: [card.resource],
    ...(card.attackRange ? { range: [card.attackRange] } : {}),
    ...ratingValues(card),
    ...statValues(card),
  }),
  keyOf: (card) => card.canonicalPath,
};
