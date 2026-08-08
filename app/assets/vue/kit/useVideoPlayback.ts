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

/** Reactive surface + the two plain (non-reactive) bits of loop bookkeeping. */
interface PlaybackState {
    videoEl: Ref<HTMLVideoElement | null>
    isPaused: Ref<boolean>
    isMuted: Ref<boolean>
    progress: Ref<number>
    rafId: number
    resumeOnVisible: boolean
}

export function useVideoPlayback(): VideoPlayback {
    const state: PlaybackState = {
        videoEl: ref(null),
        isPaused: ref(false),
        isMuted: ref(true),
        progress: ref(0),
        rafId: 0,
        resumeOnVisible: false,
    }

    const onVisibilityChange = (): void => syncWithTabVisibility(state)
    document.addEventListener('visibilitychange', onVisibilityChange)

    watch(state.videoEl, (el, prev) => adoptElement(state, el, prev))
    onBeforeUnmount(() => teardown(state, onVisibilityChange))

    return {
        videoEl: state.videoEl,
        isPaused: state.isPaused,
        isMuted: state.isMuted,
        progress: state.progress,
        toggle: () => togglePlayback(state),
        toggleMute: () => toggleMute(state),
        onPlay: () => startSampling(state),
        onPause: () => stopSampling(state),
    }
}

function syncProgress(state: PlaybackState): void {
    const video = state.videoEl.value
    if (!video) return
    if (video.duration > 0) state.progress.value = video.currentTime / video.duration
    if (!video.paused) state.rafId = requestAnimationFrame(() => syncProgress(state))
}

function startSampling(state: PlaybackState): void {
    state.isPaused.value = false
    cancelAnimationFrame(state.rafId)
    state.rafId = requestAnimationFrame(() => syncProgress(state))
}

function stopSampling(state: PlaybackState): void {
    state.isPaused.value = true
    cancelAnimationFrame(state.rafId)
    syncProgress(state)
}

function togglePlayback(state: PlaybackState): void {
    const video = state.videoEl.value
    if (!video) return
    if (video.paused) void video.play()
    else video.pause()
}

function toggleMute(state: PlaybackState): void {
    state.isMuted.value = !state.isMuted.value
    if (state.videoEl.value) state.videoEl.value.muted = state.isMuted.value
}

/**
 * A playing preview must not keep blaring from a backgrounded tab. Pause on
 * hide; resume on return only when it was actually playing, never overriding a
 * manual pause. play()/pause() fire the @play/@pause handlers, so isPaused and
 * the progress loop stay consistent.
 */
function syncWithTabVisibility(state: PlaybackState): void {
    const video = state.videoEl.value
    if (!video) return
    if (document.hidden) {
        state.resumeOnVisible = !video.paused
        if (state.resumeOnVisible) video.pause()
    } else if (state.resumeOnVisible) {
        state.resumeOnVisible = false
        void video.play()
    }
}

function adoptElement(
    state: PlaybackState,
    el: HTMLVideoElement | null,
    previous: HTMLVideoElement | null | undefined,
): void {
    // Keyed swap detaches the old element; pausing it guarantees its looping
    // audio stops immediately when moving to another spell.
    previous?.pause()
    cancelAnimationFrame(state.rafId)
    state.isPaused.value = false
    state.progress.value = 0
    // Enforce the sticky mute choice on the fresh element (attribute alone
    // would not, see the composable doc).
    if (el) el.muted = state.isMuted.value
}

function teardown(state: PlaybackState, onVisibilityChange: () => void): void {
    cancelAnimationFrame(state.rafId)
    document.removeEventListener('visibilitychange', onVisibilityChange)
    // Stop the audio on teardown (Turbo navigation unmounts the island): a
    // detached <video> can keep playing until GC in Chrome.
    state.videoEl.value?.pause()
}
