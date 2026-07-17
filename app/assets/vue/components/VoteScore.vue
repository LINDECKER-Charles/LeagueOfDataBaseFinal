<script setup lang="ts">
import { computed, ref } from 'vue'
import { applyVote, isVoteState, type VoteDirection, type VoteState } from '../community/voteState'

/**
 * Net-score vote widget for a public build. Optimistic: the score moves on
 * click and rolls back if the server disagrees (non-2xx or malformed body).
 * Anonymous visitors get the same arrows as links to the sign-in page.
 * The same-origin fetch carries the Origin header the stateless CSRF check
 * relies on; `_token` mirrors the form flow.
 */
const props = defineProps<{
    endpoint: string
    csrf: string
    score: number
    myVote: number
    loginUrl?: string | null
    labels: { up: string; down: string; score: string; login: string }
}>()

const state = ref<VoteState>({ score: props.score, myVote: props.myVote })
const isPending = ref(false)

const scoreClass = computed(() => {
    if (state.value.score > 0) return 'vote-score--positive'
    return state.value.score < 0 ? 'vote-score--negative' : 'vote-score--zero'
})
const scoreText = computed(() => (state.value.score > 0 ? `+${state.value.score}` : String(state.value.score)))

async function vote(direction: VoteDirection): Promise<void> {
    if (isPending.value) {
        return
    }
    const before = state.value
    state.value = applyVote(before, direction)
    isPending.value = true
    try {
        const response = await fetch(props.endpoint, {
            method: 'POST',
            headers: { Accept: 'application/json', 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ _token: props.csrf, value: direction === 1 ? 'up' : 'down' }),
        })
        if (!response.ok) {
            throw new Error(`vote failed: ${response.status}`)
        }
        const payload: unknown = await response.json()
        if (!isVoteState(payload)) {
            throw new Error('malformed vote payload')
        }
        state.value = payload
    } catch {
        state.value = before // rollback: the server stays authoritative
    } finally {
        isPending.value = false
    }
}
</script>

<template>
    <div class="vote-box">
        <a v-if="loginUrl" class="vote-arrow" :href="loginUrl" :title="labels.login" :aria-label="labels.login">
            <svg viewBox="0 0 12 12" fill="currentColor" aria-hidden="true"><path d="M6 2.4 10.4 9H1.6z" /></svg>
        </a>
        <button
            v-else
            type="button"
            class="vote-arrow"
            :class="{ 'vote-arrow--on-up': state.myVote === 1 }"
            :aria-pressed="state.myVote === 1"
            :disabled="isPending"
            :title="labels.up"
            :aria-label="labels.up"
            @click="vote(1)"
        >
            <svg viewBox="0 0 12 12" fill="currentColor" aria-hidden="true"><path d="M6 2.4 10.4 9H1.6z" /></svg>
        </button>

        <span class="vote-score" :class="scoreClass" :title="labels.score" aria-live="polite">{{ scoreText }}</span>

        <a v-if="loginUrl" class="vote-arrow" :href="loginUrl" :title="labels.login" :aria-label="labels.login">
            <svg viewBox="0 0 12 12" fill="currentColor" aria-hidden="true"><path d="M6 9.6 1.6 3h8.8z" /></svg>
        </a>
        <button
            v-else
            type="button"
            class="vote-arrow"
            :class="{ 'vote-arrow--on-down': state.myVote === -1 }"
            :aria-pressed="state.myVote === -1"
            :disabled="isPending"
            :title="labels.down"
            :aria-label="labels.down"
            @click="vote(-1)"
        >
            <svg viewBox="0 0 12 12" fill="currentColor" aria-hidden="true"><path d="M6 9.6 1.6 3h8.8z" /></svg>
        </button>
    </div>
</template>
