import type { Edition } from '../../../../core/api/generated/models/edition';
import type { SummonerCard } from '../../../../core/api/generated/models/summoner-card';
import type { SummonerFacets } from '../../../../core/api/generated/models/summoner-facets';
import type { Translate } from '../../items/codex/texts/translate';
import { defineFacet } from '../../shared/facets/define-facet';
import type { FacetDefinition } from '../../shared/facets/model/facet-definition';
import type { FacetOption } from '../../shared/facets/model/facet-option';

const EDITIONS: readonly Edition[] = ['modern', 'classic'];
const COOLDOWN_UNIT = 's';

// The facet offers mode codes; the cards carry the whitelist's label of each.
function modeOptions(facets: SummonerFacets, cards: readonly SummonerCard[]): FacetOption[] {
  const modes = cards.flatMap((card) => card.modes);
  const labels = new Map(modes.map((mode) => [mode.code, mode.label]));
  return facets.modes.map((code) => ({ value: code, label: labels.get(code) ?? code }));
}

// The options the data decides: the modes, labelled, and the unlock levels.
interface DataOptions {
  readonly modes: FacetOption[];
  readonly levels: FacetOption[];
}

function availabilityFacets(options: DataOptions, t: Translate): FacetDefinition[] {
  const group = t('facet.group.availability');
  const editions = EDITIONS.map((value) => ({ value, label: t(`edition.${value}`) }));
  return [
    defineFacet({
      key: 'mode',
      kind: 'choice',
      label: t('facet.summoner.mode'),
      group,
      options: options.modes,
      primary: true,
    }),
    defineFacet({
      key: 'edition',
      kind: 'choice',
      label: t('facet.summoner.edition'),
      group,
      options: editions,
      primary: true,
      multiple: false,
    }),
    defineFacet({
      key: 'level',
      kind: 'choice',
      label: t('facet.summoner.level'),
      group,
      options: options.levels,
    }),
  ];
}

/**
 * The filters of the summoner spell list, translated: the modes, the edition and the unlock
 * levels the data holds, then the cooldown in seconds.
 */
export function summonerFacetsOf(
  facets: SummonerFacets,
  cards: readonly SummonerCard[],
  t: Translate,
): FacetDefinition[] {
  const cooldown = defineFacet({
    key: 'cooldown',
    kind: 'range',
    label: t('facet.summoner.cooldown'),
    group: t('facet.group.stats'),
    unit: COOLDOWN_UNIT,
  });
  const modes = modeOptions(facets, cards);
  const levels = facets.levels.map((level) => ({ value: String(level), label: String(level) }));
  return [...availabilityFacets({ modes, levels }, t), cooldown];
}
