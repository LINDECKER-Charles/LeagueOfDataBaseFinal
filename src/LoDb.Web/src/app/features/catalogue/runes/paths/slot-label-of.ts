import type { Translate } from '../../items/codex/texts/translate';
import { rowNumberOf } from './row-number-of';

/** The name of a row of a rune path: "Keystone", then "Row 1" to "Row 3". */
export function slotLabelOf(slot: string, t: Translate): string {
  const row = rowNumberOf(slot);
  return row === null ? t('facet.rune.slot_keystone') : t('facet.rune.slot_row', { n: row });
}
