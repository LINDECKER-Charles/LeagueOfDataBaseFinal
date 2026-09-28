import { describe, expect, it } from 'vitest'
import { applyVote, isVoteState } from './voteState'

describe('applyVote', () => {
    it('casts a fresh upvote', () => {
        expect(applyVote({ score: 4, myVote: 0 }, 1)).toEqual({ score: 5, myVote: 1 })
    })

    it('casts a fresh downvote', () => {
        expect(applyVote({ score: 4, myVote: 0 }, -1)).toEqual({ score: 3, myVote: -1 })
    })

    it('withdraws the vote when re-voting the same direction (toggle)', () => {
        expect(applyVote({ score: 5, myVote: 1 }, 1)).toEqual({ score: 4, myVote: 0 })
        expect(applyVote({ score: 3, myVote: -1 }, -1)).toEqual({ score: 4, myVote: 0 })
    })

    it('switching direction swings the score by two', () => {
        expect(applyVote({ score: 5, myVote: 1 }, -1)).toEqual({ score: 3, myVote: -1 })
        expect(applyVote({ score: 3, myVote: -1 }, 1)).toEqual({ score: 5, myVote: 1 })
    })

    it('never mutates the input state', () => {
        const state = { score: 2, myVote: 0 }
        applyVote(state, 1)
        expect(state).toEqual({ score: 2, myVote: 0 })
    })
})

describe('isVoteState', () => {
    it('accepts the server payload shape', () => {
        expect(isVoteState({ score: -3, myVote: -1 })).toBe(true)
        expect(isVoteState({ score: 0, myVote: 0 })).toBe(true)
    })

    it('rejects malformed payloads', () => {
        expect(isVoteState(null)).toBe(false)
        expect(isVoteState('ok')).toBe(false)
        expect(isVoteState({ score: 'high', myVote: 0 })).toBe(false)
        expect(isVoteState({ score: 1, myVote: 2 })).toBe(false)
        expect(isVoteState({ score: Number.NaN, myVote: 0 })).toBe(false)
    })
})
