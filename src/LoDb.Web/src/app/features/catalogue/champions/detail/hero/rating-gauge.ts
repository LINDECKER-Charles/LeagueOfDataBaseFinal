import type { ChampionRatings } from '../../../../../core/api/generated/models/champion-ratings';

/** One of Riot's 0 to 10 ratings, as the hero draws it. */
export interface RatingGauge {
  readonly key: keyof ChampionRatings;
  readonly value: number;
}
