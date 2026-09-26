import type { VoteState } from '../../../../core/api/generated/models/vote-state';
import type { VoteDirection } from './vote-direction';

/**
 * What the API will answer for a vote, predicted for the optimistic update: the same
 * direction again withdraws the vote, the other one replaces it, and the net score moves
 * accordingly. The API's answer, of the same shape, stays authoritative.
 */
export function applyVote(state: VoteState, direction: VoteDirection): VoteState {
  const myVote = state.myVote === direction ? 0 : direction;
  return { score: state.score - state.myVote + myVote, myVote };
}
