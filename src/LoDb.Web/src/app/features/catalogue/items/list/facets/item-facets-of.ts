import type { Edition } from '../../../../../core/api/generated/models/edition';
import type { ItemTier } from '../../../../../core/api/generated/models/item-tier';
import { defineFacet } from '../../../shared/facets/define-facet';
import type { FacetDefinition } from '../../../shared/facets/model/facet-definition';
import type { FacetOption } from '../../../shared/facets/model/facet-option';
import type { Translate } from '../../../shared/codex/texts/translate';
import { ITEM_STAT_COLUMNS } from '../../stats/item-stat-columns';
import { itemStatKey } from '../../stats/item-stat-key';
import { ITEM_MAPS } from './item-maps';
import { tagLabelOf } from './tag-label-of';

const EDITIONS: readonly Edition[] = ['modern', 'classic'];
const TIERS: readonly NonNullable<ItemTier>[] = ['component', 'epic', 'legendary'];
const PERCENT_UNIT = '%';

// Humanised like the card chips, then sorted by what the reader sees.
function tagOptions(tags: readonly string[]): FacetOption[] {
  return tags
    .map((tag) => ({ value: tag, label: tagLabelOf(tag) }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

function identityFacets(tags: readonly string[], t: Translate): FacetDefinition[] {
  const group = t('facet.group.identity');
  return [
    defineFacet({
      key: 'tag',
      kind: 'choice',
      label: t('facet.item.tag'),
      group,
      options: tagOptions(tags),
      primary: true,
      matchAll: true,
    }),
    defineFacet({
      key: 'edition',
      kind: 'choice',
      label: t('facet.item.edition'),
      group,
      options: EDITIONS.map((value) => ({ value, label: t(`edition.${value}`) })),
      primary: true,
      multiple: false,
    }),
    defineFacet({
      key: 'tier',
      kind: 'choice',
      label: t('facet.item.tier'),
      group,
      options: TIERS.map((value) => ({ value, label: t(`facet.item.tiers.${value}`) })),
    }),
  ];
}

function availabilityFacets(t: Translate): FacetDefinition[] {
  const group = t('facet.group.availability');
  const maps = ITEM_MAPS.map((id) => ({ value: String(id), label: t(`map.${id}`) }));
  return [
    defineFacet({ key: 'map', kind: 'choice', label: t('facet.item.map'), group, options: maps }),
    defineFacet({ key: 'purchasable', kind: 'toggle', label: t('facet.item.purchasable'), group }),
    defineFacet({ key: 'consumable', kind: 'toggle', label: t('facet.item.consumable'), group }),
  ];
}

function economyAndStatFacets(t: Translate): FacetDefinition[] {
  const stats = t('facet.group.stats');
  return [
    defineFacet({
      key: 'price',
      kind: 'range',
      label: t('facet.item.price'),
      group: t('facet.group.economy'),
    }),
    ...ITEM_STAT_COLUMNS.map((column) =>
      defineFacet({
        key: itemStatKey(column),
        kind: 'range',
        label: t(`stat.${column.stat}`),
        group: stats,
        unit: column.isPercent ? PERCENT_UNIT : null,
      }),
    ),
  ];
}

/**
 * The filters of the item list, translated: category (the tags the list carries), edition
 * and tier; map, purchasable and consumable; price, then one range per stat.
 */
export function itemFacetsOf(tags: readonly string[], t: Translate): FacetDefinition[] {
  return [...identityFacets(tags, t), ...availabilityFacets(t), ...economyAndStatFacets(t)];
}
