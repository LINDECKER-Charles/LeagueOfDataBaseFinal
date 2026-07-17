/**
 * Pure vote-state rules of the net-score widget. Mirrors the server's toggle
 * semantics (BuildVoteRepository::applyVote): voting the same direction again
 * withdraws the vote, voting the other direction replaces it. Used for the
 * optimistic update; the server response (same shape) remains authoritative.
 */

export type VoteDirection = 1 | -1

export interface VoteState {
    /** Net score (sum of all votes) — the only number ever shown. */
    score: number
    /** The viewer's own vote: 1, -1 or 0 (none). */
    myVote: number
}

/** Local prediction of what the server will answer for this vote request. */
export function applyVote(state: VoteState, direction: VoteDirection): VoteState {
    const nextVote = state.myVote === direction ? 0 : direction
    return { score: state.score - state.myVote + nextVote, myVote: nextVote }
}

/** Runtime guard for the server payload — anything malformed triggers a rollback. */
export function isVoteState(value: unknown): value is VoteState {
    if (typeof value !== 'object' || value === null) {
        return false
    }
    const candidate = value as Record<string, unknown>
    return (
        typeof candidate.score === 'number' &&
        Number.isFinite(candidate.score) &&
        (candidate.myVote === 0 || candidate.myVote === 1 || candidate.myVote === -1)
    )
}
