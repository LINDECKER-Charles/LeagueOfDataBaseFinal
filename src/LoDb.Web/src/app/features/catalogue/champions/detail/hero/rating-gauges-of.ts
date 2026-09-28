import type { ChampionRatings } from '../../../../../core/api/generated/models/champion-ratings';
import type { RatingGauge } from './rating-gauge';

const ORDER: readonly (keyof ChampionRatings)[] = ['attack', 'defense', 'magic', 'difficulty'];

/** Riot's ratings in the order players read them; none when the champion has none. */
export function ratingGaugesOf(ratings: ChampionRatings | null | undefined): RatingGauge[] {
  return ratings ? ORDER.map((key) => ({ key, value: ratings[key] })) : [];
}
