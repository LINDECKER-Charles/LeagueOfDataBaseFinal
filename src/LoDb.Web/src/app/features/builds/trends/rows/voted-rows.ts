import type { TrendRow } from '../../../../core/api/generated/models/trend-row';
import type { VoteState } from '../../../../core/api/generated/models/vote-state';
import type { VotedRow } from './voted-row';

/**
 * The rows of the trends with their scores: the one read again for the signed-in reader when
 * there is one, else the row's own, anonymous as the server rendered it.
 */
export function votedRows(
  rows: readonly TrendRow[],
  votes: ReadonlyMap<number, VoteState>,
): VotedRow[] {
  return rows.map((row) => ({
    row,
    vote: votes.get(row.id) ?? { score: row.score, myVote: row.myVote ?? 0 },
  }));
}
