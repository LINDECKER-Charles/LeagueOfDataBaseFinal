import type { ChampionAbility } from '../../../../../core/api/generated/models/champion-ability';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import type { ItemDetails } from '../../../../../core/api/generated/models/item-details';
import type { RuneEntry } from '../../../../../core/api/generated/models/rune-entry';

/**
 * What the blocks of an entity's states read of a detail of any resource: its state, history,
 * next state and changes, and the sub-entities that name the fields of its history (a
 * champion's spells and skins, a path's runes), which the API leaves unnamed there.
 */
export interface StatefulDetails extends Pick<
  ItemDetails,
  'canonicalPath' | 'canonical' | 'state' | 'history' | 'next' | 'presentInLatest' | 'changes'
> {
  readonly abilities?: readonly Pick<ChampionAbility, 'slot' | 'name'>[];
  readonly skins?: readonly Pick<ChampionSkin, 'id' | 'name'>[] | null;
  readonly slots?: readonly { readonly runes: readonly Pick<RuneEntry, 'id' | 'name'>[] }[];
}
