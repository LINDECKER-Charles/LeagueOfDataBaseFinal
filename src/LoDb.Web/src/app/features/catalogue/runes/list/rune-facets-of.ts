import type { RuneTreeCard } from '../../../../core/api/generated/models/rune-tree-card';
import type { Translate } from '../../shared/codex/texts/translate';
import { defineFacet } from '../../shared/facets/define-facet';
import type { FacetDefinition } from '../../shared/facets/model/facet-definition';
import { RUNE_SLOTS } from '../paths/rune-slots';
import { slotLabelOf } from '../paths/slot-label-of';

/** The filters of the rune list, translated: its path (the list's own), and its row. */
export function runeFacetsOf(trees: readonly RuneTreeCard[], t: Translate): FacetDefinition[] {
  const group = t('facet.group.identity');
  return [
    defineFacet({
      key: 'path',
      kind: 'choice',
      label: t('facet.rune.path'),
      group,
      options: trees.map((tree) => ({ value: tree.key, label: tree.name })),
      primary: true,
    }),
    defineFacet({
      key: 'slot',
      kind: 'choice',
      label: t('facet.rune.slot'),
      group,
      options: RUNE_SLOTS.map((slot) => ({ value: slot, label: slotLabelOf(slot, t) })),
      primary: true,
    }),
  ];
}
