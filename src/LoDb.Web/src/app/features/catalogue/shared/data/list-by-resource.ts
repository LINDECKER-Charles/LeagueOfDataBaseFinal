import type { ChampionList } from '../../../../core/api/generated/models/champion-list';
import type { ItemList } from '../../../../core/api/generated/models/item-list';
import type { RuneList } from '../../../../core/api/generated/models/rune-list';
import type { SummonerList } from '../../../../core/api/generated/models/summoner-list';

/** The list the API answers for each resource of the catalogue. */
export interface ListByResource {
  readonly champions: ChampionList;
  readonly items: ItemList;
  readonly runes: RuneList;
  readonly summoners: SummonerList;
}
