import { applyVote } from './apply-vote';
import { isVoteState } from './is-vote-state';

// The cases of the legacy spec (assets/vue/community/voteState.spec.ts).
describe('applyVote', () => {
  it('casts a fresh upvote', () => {
    expect(applyVote({ score: 4, myVote: 0 }, 1)).toEqual({ score: 5, myVote: 1 });
  });

  it('casts a fresh downvote', () => {
    expect(applyVote({ score: 4, myVote: 0 }, -1)).toEqual({ score: 3, myVote: -1 });
  });

  it('withdraws the vote when voting the same direction again', () => {
    expect(applyVote({ score: 5, myVote: 1 }, 1)).toEqual({ score: 4, myVote: 0 });
    expect(applyVote({ score: 3, myVote: -1 }, -1)).toEqual({ score: 4, myVote: 0 });
  });

  it('swings the score by two when switching direction', () => {
    expect(applyVote({ score: 5, myVote: 1 }, -1)).toEqual({ score: 3, myVote: -1 });
    expect(applyVote({ score: 3, myVote: -1 }, 1)).toEqual({ score: 5, myVote: 1 });
  });

  it('never mutates the state it is given', () => {
    const state = { score: 2, myVote: 0 };

    applyVote(state, 1);

    expect(state).toEqual({ score: 2, myVote: 0 });
  });
});

describe('isVoteState', () => {
  it('accepts the shape the API answers', () => {
    expect(isVoteState({ score: -3, myVote: -1 })).toBe(true);
    expect(isVoteState({ score: 0, myVote: 0 })).toBe(true);
  });

  it.each([
    ['null', null],
    ['a string', 'ok'],
    ['a score that is not a number', { score: 'high', myVote: 0 }],
    ['a vote out of range', { score: 1, myVote: 2 }],
    ['a score that is not finite', { score: Number.NaN, myVote: 0 }],
  ])('refuses %s', (_case, value) => {
    expect(isVoteState(value)).toBe(false);
  });
});
