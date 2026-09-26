import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import { defineFacet } from '../../shared/facets/define-facet';
import type { FacetDefinition } from '../../shared/facets/model/facet-definition';
import { CHAMPION_BASE_STATS } from './champion-base-stats';
import { resourceOptions } from './resource-options';

type Translate = (key: string) => string;

/** Data Dragon's roles, in the order players know them. */
const ROLES = ['Fighter', 'Tank', 'Mage', 'Assassin', 'Marksman', 'Support'];
const RANGES = ['melee', 'ranged'];
const RATINGS = ['difficulty', 'attack', 'defense', 'magic'];
/** The attack speed reads in hundredths (0.625); every other stat in units. */
const ATTACK_SPEED_STEP = 0.01;

function profileFacets(translate: Translate, cards: readonly ChampionCard[]): FacetDefinition[] {
  const group = translate('facet.group.profile');
  const choice = (key: string, options: FacetDefinition['options']) =>
    defineFacet({
      key,
      kind: 'choice',
      label: translate(`facet.champion.${key}`),
      group,
      options,
      primary: true,
    });
  return [
    choice(
      'role',
      ROLES.map((role) => ({
        value: role,
        label: translate(`facet.champion.roles.${role.toLowerCase()}`),
      })),
    ),
    choice('resource', resourceOptions(cards, translate('facet.champion.resource_none'))),
    choice(
      'range',
      RANGES.map((range) => ({ value: range, label: translate(`facet.champion.ranges.${range}`) })),
    ),
  ];
}

/**
 * The filters of the champions list, translated: roles, resource and range as choices, Riot's
 * ratings and the level-1 stats as ranges. The resource options come from the cards, since
 * each patch has its own set.
 */
export function championFacets(
  translate: Translate,
  cards: readonly ChampionCard[],
): FacetDefinition[] {
  const ratings = translate('facet.group.ratings');
  const stats = translate('facet.group.base_stats');
  return [
    ...profileFacets(translate, cards),
    ...RATINGS.map((key) =>
      defineFacet({
        key,
        kind: 'range',
        label: translate(`facet.champion.${key}`),
        group: ratings,
      }),
    ),
    ...CHAMPION_BASE_STATS.map(({ key, stat }) =>
      defineFacet({
        key,
        kind: 'range',
        label: translate(`stat.${stat}`),
        group: stats,
        step: stat === 'attack_speed' ? ATTACK_SPEED_STEP : 1,
      }),
    ),
  ];
}
