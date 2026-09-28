import type { SummonerMode } from '../../../../core/api/generated/models/summoner-mode';

/** The mode of the LoL Classic client: an edition, named as such rather than as a queue. */
const CLASSIC_MODE = 'JADE';

/**
 * The names of the modes a summoner spell is allowed in, in the API's order: the label the
 * API's whitelist gives, or the edition's name for LoL Classic. A mode the whitelist does not
 * name is dropped, as is a name already shown.
 */
export function modeLabelsOf(modes: readonly SummonerMode[], classicLabel: string): string[] {
  const labels = modes.map(
    (mode) => mode.label ?? (mode.code === CLASSIC_MODE ? classicLabel : null),
  );
  return [...new Set(labels.filter((label): label is string => !!label))];
}
