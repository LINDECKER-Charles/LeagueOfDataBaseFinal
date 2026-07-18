import { ref, watch, onBeforeUnmount, type Ref } from 'vue'

/**
 * Play/pause + mute toggle + playback progress for a looping, chrome-less
 * <video>. Progress is sampled on requestAnimationFrame only while the video
 * plays (timeupdate is too coarse for a smooth bar); the loop stops on pause so
 * an idle page schedules nothing. The element ref is expected to be recreated
 * per media swap (keyed <video>), which resets state via the watcher — and the
 * previous element is paused there so its looping audio cuts on slot change.
 *
 * Mute is owned here as a property, not the `muted` content attribute: Vue only
 * patches the attribute (vuejs/core#3057), leaving the IDL property false, so a
 * template-level `muted` lets the loop play out loud. Starting muted also keeps
 * autoplay within browser policy; the user opts into sound, and the choice is
 * sticky across slots.
 */
export interface VideoPlayback {
    videoEl: Ref<HTMLVideoElement | null>
    isPaused: Ref<boolean>
    isMuted: Ref<boolean>
    /** Playback position as a 0..1 fraction of duration. */
    progress: Ref<number>
    toggle: () => void
    toggleMute: () => void
    onPlay: () => void
    onPause: () => void
}

export function useVideoPlayback(): VideoPlayback {
    const videoEl = ref<HTMLVideoElement | null>(null)
    const isPaused = ref(false)
    const isMuted = ref(true)
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

    const toggleMute = (): void => {
        isMuted.value = !isMuted.value
        if (videoEl.value) videoEl.value.muted = isMuted.value
    }

    watch(videoEl, (el, prev) => {
        // Keyed swap detaches the old element; pausing it guarantees its looping
        // audio stops immediately when moving to another spell.
        prev?.pause()
        cancelAnimationFrame(rafId)
        isPaused.value = false
        progress.value = 0
        // Enforce the sticky mute choice on the fresh element (attribute alone
        // would not, see class doc).
        if (el) el.muted = isMuted.value
    })

    onBeforeUnmount(() => cancelAnimationFrame(rafId))

    return { videoEl, isPaused, isMuted, progress, toggle, toggleMute, onPlay, onPause }
}
