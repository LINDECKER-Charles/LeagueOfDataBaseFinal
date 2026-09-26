import type { ChampionDetails } from '../../api/generated/models/champion-details';
import type { ItemDetails } from '../../api/generated/models/item-details';
import type { RuneTreeDetails } from '../../api/generated/models/rune-tree-details';
import type { SummonerDetails } from '../../api/generated/models/summoner-details';

/** The details the API answers for each resource of the catalogue. */
export interface DetailsByResource {
  readonly champions: ChampionDetails;
  readonly items: ItemDetails;
  readonly runes: RuneTreeDetails;
  readonly summoners: SummonerDetails;
}
