import { mount } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { defineComponent, nextTick, shallowRef, type ShallowRef } from 'vue'
import { useVideoPlayback, type VideoPlayback } from './useVideoPlayback'

/** Fake <video> — the composable only reads/writes this surface. */
function fakeVideo(overrides: Partial<HTMLVideoElement> = {}): HTMLVideoElement {
    return {
        paused: false,
        duration: 10,
        currentTime: 0,
        play: vi.fn(),
        pause: vi.fn(),
        ...overrides,
    } as unknown as HTMLVideoElement
}

let rafQueue = new Map<number, FrameRequestCallback>()
let rafSeq = 0
function flushRaf(): void {
    const callbacks = [...rafQueue.values()]
    rafQueue = new Map()
    callbacks.forEach((cb) => cb(0))
}

interface Harness {
    playback: VideoPlayback
    videoEl: ShallowRef<HTMLVideoElement | null>
    unmount: () => void
}

function mountPlayback(): Harness {
    let playback!: VideoPlayback
    const videoEl = shallowRef<HTMLVideoElement | null>(null)
    const harness = defineComponent({
        setup() {
            playback = useVideoPlayback(videoEl)
            return () => null
        },
    })
    const wrapper = mount(harness)
    return { playback, videoEl, unmount: () => wrapper.unmount() }
}

describe('useVideoPlayback', () => {
    beforeEach(() => {
        rafQueue = new Map()
        vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
            rafQueue.set(++rafSeq, cb)
            return rafSeq
        })
        vi.stubGlobal('cancelAnimationFrame', vi.fn((id: number) => rafQueue.delete(id)))
    })
    afterEach(() => {
        vi.unstubAllGlobals()
        vi.restoreAllMocks()
    })

    it('toggle() plays a paused video and pauses a playing one', () => {
        const { playback, videoEl } = mountPlayback()
        const video = fakeVideo({ paused: true })
        videoEl.value = video

        playback.toggle()
        expect(video.play).toHaveBeenCalledOnce()

        ;(video as { paused: boolean }).paused = false
        playback.toggle()
        expect(video.pause).toHaveBeenCalledOnce()
    })

    it('tracks progress on animation frames while playing, stops after pause', () => {
        const { playback, videoEl } = mountPlayback()
        const video = fakeVideo({ currentTime: 2.5 })
        videoEl.value = video

        playback.onPlay()
        expect(playback.isPaused.value).toBe(false)
        flushRaf()
        expect(playback.progress.value).toBe(0.25)

        ;(video as { currentTime: number }).currentTime = 5
        flushRaf()
        expect(playback.progress.value).toBe(0.5)

        ;(video as { paused: boolean }).paused = true
        playback.onPause()
        expect(playback.isPaused.value).toBe(true)
        expect(rafQueue.size).toBe(0)
    })

    it('onPause() syncs the final position once', () => {
        const { playback, videoEl } = mountPlayback()
        const video = fakeVideo({ paused: true, currentTime: 7.5 })
        videoEl.value = video

        playback.onPause()
        expect(playback.progress.value).toBe(0.75)
        expect(rafQueue.size).toBe(0)
    })

    it('keeps the last progress while duration is unknown', () => {
        const { playback, videoEl } = mountPlayback()
        videoEl.value = fakeVideo({ duration: NaN, currentTime: 3 })

        playback.onPlay()
        flushRaf()
        expect(playback.progress.value).toBe(0)
    })

    it('resets state and pauses the previous element when swapped (keyed re-render)', async () => {
        const { playback, videoEl } = mountPlayback()
        const prev = fakeVideo({ paused: true, currentTime: 5 })
        videoEl.value = prev
        await nextTick() // flush so the next swap sees `prev` as the old value
        playback.onPause()
        expect(playback.progress.value).toBe(0.5)

        videoEl.value = fakeVideo()
        await nextTick()
        expect(prev.pause).toHaveBeenCalledOnce()
        expect(playback.progress.value).toBe(0)
        expect(playback.isPaused.value).toBe(false)
    })

    it('starts muted and enforces the sticky mute state on each swapped element', async () => {
        const { playback, videoEl } = mountPlayback()
        expect(playback.isMuted.value).toBe(true)

        const first = fakeVideo({ muted: false })
        videoEl.value = first
        await nextTick()
        expect(first.muted).toBe(true)

        playback.toggleMute()
        expect(playback.isMuted.value).toBe(false)
        expect(first.muted).toBe(false)

        const next = fakeVideo({ muted: true })
        videoEl.value = next
        await nextTick()
        expect(next.muted).toBe(false)
    })

    it('cancels the frame loop on unmount', () => {
        const { playback, videoEl, unmount } = mountPlayback()
        videoEl.value = fakeVideo()
        playback.onPlay()
        flushRaf()

        unmount()
        expect(cancelAnimationFrame).toHaveBeenCalled()
    })

    it('pauses the current video on unmount so detached audio stops', () => {
        const { videoEl, unmount } = mountPlayback()
        const video = fakeVideo()
        videoEl.value = video

        unmount()
        expect(video.pause).toHaveBeenCalled()
    })

    it('pauses a playing video when the tab hides and resumes it on return', () => {
        const { videoEl } = mountPlayback()
        const video = fakeVideo({ paused: false })
        videoEl.value = video
        const hidden = vi.spyOn(document, 'hidden', 'get')

        hidden.mockReturnValue(true)
        document.dispatchEvent(new Event('visibilitychange'))
        expect(video.pause).toHaveBeenCalledOnce()

        ;(video as { paused: boolean }).paused = true
        hidden.mockReturnValue(false)
        document.dispatchEvent(new Event('visibilitychange'))
        expect(video.play).toHaveBeenCalledOnce()
    })

    it('does not resume a video the user paused before hiding the tab', () => {
        const { videoEl } = mountPlayback()
        const video = fakeVideo({ paused: true })
        videoEl.value = video
        const hidden = vi.spyOn(document, 'hidden', 'get')

        hidden.mockReturnValue(true)
        document.dispatchEvent(new Event('visibilitychange'))
        expect(video.pause).not.toHaveBeenCalled()

        hidden.mockReturnValue(false)
        document.dispatchEvent(new Event('visibilitychange'))
        expect(video.play).not.toHaveBeenCalled()
    })
})
