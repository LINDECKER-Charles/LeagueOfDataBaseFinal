import type { SummonerCard } from '../../../../core/api/generated/models/summoner-card';
import type { CardValues } from '../../shared/facets/model/card-values';
import type { CatalogueCardAdapter } from '../../shared/state/catalogue-card-adapter';

// The facets read the first rank's cooldown and the unlock level only where the spell has them.
function optionalValuesOf(card: SummonerCard): CardValues {
  const [cooldown] = card.cooldown;
  return {
    ...(cooldown === undefined ? {} : { cooldown }),
    ...(typeof card.summonerLevel === 'number' ? { level: [String(card.summonerLevel)] } : {}),
  };
}

/**
 * How the summoner spell list filters its cards, by id: the search reads the name and the
 * id, as the legacy list did; the facets read the modes the mode facet offers, the edition,
 * the unlock level and the cooldown.
 */
export const SUMMONER_CARD_ADAPTER: CatalogueCardAdapter<SummonerCard> = {
  searchTextOf: (card) => `${card.name} ${card.id}`,
  valuesOf: (card) => ({
    mode: card.modes.filter((mode) => mode.facetable).map((mode) => mode.code),
    edition: [card.edition],
    ...optionalValuesOf(card),
  }),
  keyOf: (card) => card.id,
};
