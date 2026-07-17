import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import VoteScore from './VoteScore.vue'

const LABELS = { up: 'Upvote', down: 'Downvote', score: 'Score', login: 'Sign in to vote' }

function mountWidget(overrides: Record<string, unknown> = {}): VueWrapper {
    return mount(VoteScore, {
        props: {
            endpoint: '/builds/7/vote',
            csrf: 'token',
            score: 3,
            myVote: 0,
            loginUrl: null,
            labels: LABELS,
            ...overrides,
        },
    })
}

function jsonResponse(body: unknown, status = 200): Response {
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

afterEach(() => {
    vi.unstubAllGlobals()
})

describe('VoteScore', () => {
    it('shows the signed net score', () => {
        expect(mountWidget().find('.vote-score').text()).toBe('+3')
        expect(mountWidget({ score: -2 }).find('.vote-score').text()).toBe('-2')
        expect(mountWidget({ score: 0 }).find('.vote-score').text()).toBe('0')
    })

    it('optimistically bumps then keeps the server value', async () => {
        const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ score: 4, myVote: 1 }))
        vi.stubGlobal('fetch', fetchMock)

        const wrapper = mountWidget()
        await wrapper.findAll('button')[0]!.trigger('click')
        expect(wrapper.find('.vote-score').text()).toBe('+4') // optimistic
        await flushPromises()

        expect(wrapper.find('.vote-score').text()).toBe('+4')
        expect(wrapper.findAll('button')[0]!.attributes('aria-pressed')).toBe('true')
        const [url, init] = fetchMock.mock.calls[0]!
        expect(url).toBe('/builds/7/vote')
        expect(String(init.body)).toBe('_token=token&value=up')
        expect(init.headers.Accept).toBe('application/json')
    })

    it('rolls back the optimistic update when the server rejects', async () => {
        // Deferred response: the optimistic state must be observable BEFORE the
        // server answers, then the 403 rolls it back.
        let answer!: (response: Response) => void
        vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise<Response>((resolve) => (answer = resolve))))

        const wrapper = mountWidget()
        await wrapper.findAll('button')[1]!.trigger('click')
        expect(wrapper.find('.vote-score').text()).toBe('+2') // optimistic down

        answer(jsonResponse({ error: 'nope' }, 403))
        await flushPromises()

        expect(wrapper.find('.vote-score').text()).toBe('+3') // rolled back
        expect(wrapper.findAll('button')[1]!.attributes('aria-pressed')).toBe('false')
    })

    it('rolls back on a malformed server payload', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({ score: 'many', myVote: 9 })))

        const wrapper = mountWidget({ score: 1, myVote: 1 })
        await wrapper.findAll('button')[0]!.trigger('click') // toggle off attempt
        await flushPromises()

        expect(wrapper.find('.vote-score').text()).toBe('+1')
    })

    it('highlights my current vote and reflects a toggle-off answer', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({ score: 0, myVote: 0 })))

        const wrapper = mountWidget({ score: 1, myVote: 1 })
        expect(wrapper.findAll('button')[0]!.classes()).toContain('vote-arrow--on-up')

        await wrapper.findAll('button')[0]!.trigger('click')
        await flushPromises()

        expect(wrapper.find('.vote-score').text()).toBe('0')
        expect(wrapper.findAll('button')[0]!.classes()).not.toContain('vote-arrow--on-up')
    })

    it('renders sign-in links instead of buttons for anonymous visitors', () => {
        const wrapper = mountWidget({ loginUrl: '/login' })

        expect(wrapper.findAll('button')).toHaveLength(0)
        const links = wrapper.findAll('a')
        expect(links).toHaveLength(2)
        expect(links[0]!.attributes('href')).toBe('/login')
        expect(links[0]!.attributes('title')).toBe(LABELS.login)
    })
})
