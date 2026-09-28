import type { VoteState } from '../../../../core/api/generated/models/vote-state';

const VOTES: readonly unknown[] = [-1, 0, 1];

/** Whether an answer of the API has the shape of a vote state: anything else rolls back. */
export function isVoteState(value: unknown): value is VoteState {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const { score, myVote } = value as Record<string, unknown>;
  return typeof score === 'number' && Number.isFinite(score) && VOTES.includes(myVote);
}
