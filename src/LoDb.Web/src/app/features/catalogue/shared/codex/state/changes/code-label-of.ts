import type { ChangeField } from '../../../../../../core/api/generated/models/change-field';
import { ITEM_MAPS } from '../../../../items/list/facets/item-maps';
import { tagLabelOf } from '../../../../items/list/facets/tag-label-of';
import type { Translate } from '../../texts/translate';

/** The six roles Riot gives champions, each labelled by the champions' facet. */
const ROLES: readonly string[] = ['fighter', 'tank', 'mage', 'assassin', 'marksman', 'support'];

/**
 * A code as the page names it where the front has a table for it: a champion's role, an
 * item's map or category. Any other code (a summoner spell's mode, a code no table knows)
 * shows as Data Dragon writes it.
 */
export function codeLabelOf(field: ChangeField, code: string, translate: Translate): string {
  switch (field) {
    case 'roles': {
      const role = code.toLowerCase();
      return ROLES.includes(role) ? translate(`facet.champion.roles.${role}`) : code;
    }
    case 'maps':
      return ITEM_MAPS.includes(Number(code)) ? translate(`map.${code}`) : code;
    case 'tags':
      return tagLabelOf(code);
    default:
      return code;
  }
}
