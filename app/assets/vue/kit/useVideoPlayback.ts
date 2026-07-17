import { ref, watch, onBeforeUnmount, type Ref } from 'vue'

/**
 * Play/pause toggle + playback progress for a looping, chrome-less <video>.
 * Progress is sampled on requestAnimationFrame only while the video plays
 * (timeupdate is too coarse for a smooth bar); the loop stops on pause so an
 * idle page schedules nothing. The element ref is expected to be recreated
 * per media swap (keyed <video>), which resets state via the watcher.
 */
export interface VideoPlayback {
    videoEl: Ref<HTMLVideoElement | null>
    isPaused: Ref<boolean>
    /** Playback position as a 0..1 fraction of duration. */
    progress: Ref<number>
    toggle: () => void
    onPlay: () => void
    onPause: () => void
}

export function useVideoPlayback(): VideoPlayback {
    const videoEl = ref<HTMLVideoElement | null>(null)
    const isPaused = ref(false)
    const progress = ref(0)
    let rafId = 0

    const syncProgress = (): void => {
        const video = videoEl.value
        if (!video) return
        if (video.duration > 0) progress.value = video.currentTime / video.duration
        if (!video.paused) rafId = requestAnimationFrame(syncProgress)
    }

    const onPlay = (): void => {
        isPaused.value = false
        cancelAnimationFrame(rafId)
        rafId = requestAnimationFrame(syncProgress)
    }

    const onPause = (): void => {
        isPaused.value = true
        cancelAnimationFrame(rafId)
        syncProgress()
    }

    const toggle = (): void => {
        const video = videoEl.value
        if (!video) return
        if (video.paused) void video.play()
        else video.pause()
    }

    watch(videoEl, () => {
        cancelAnimationFrame(rafId)
        isPaused.value = false
        progress.value = 0
    })

    onBeforeUnmount(() => cancelAnimationFrame(rafId))

    return { videoEl, isPaused, progress, toggle, onPlay, onPause }
}
