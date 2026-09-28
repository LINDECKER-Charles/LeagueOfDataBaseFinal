import type { TrendRow } from '../../../../core/api/generated/models/trend-row';
import type { VoteState } from '../../../../core/api/generated/models/vote-state';

/** A build of the trends and the score to show for it. */
export interface VotedRow {
  readonly row: TrendRow;
  readonly vote: VoteState;
}
