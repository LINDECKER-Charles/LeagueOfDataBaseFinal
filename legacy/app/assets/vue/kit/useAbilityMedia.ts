import { reactive } from 'vue'
import { prefersReducedMotion } from '../fx/reducedMotion'

export type AbilitySlot = 'P' | 'Q' | 'W' | 'E' | 'R'

/**
 * Riot hosts a looping preview per ability on the CDN backing the official
 * champion pages (webm + mp4 + jpg poster), keyed by the champion's numeric
 * key zero-padded to four digits. Hotlinked like the splash art — the assumed
 * perf trade-off of this codebase. Availability is probed by the <video>
 * element itself: a load error flags the slot and the UI falls back to the
 * spell icon.
 */
const CDN_BASE = 'https://d28xe8vt774jo5.cloudfront.net/champion-abilities'

export interface AbilityMedia {
    webm: (slot: AbilitySlot) => string
    mp4: (slot: AbilitySlot) => string
    poster: (slot: AbilitySlot) => string
    markUnavailable: (slot: AbilitySlot) => void
    isAvailable: (slot: AbilitySlot) => boolean
    /**
     * Read once, when the island mounts — deliberately NOT reactive: the island
     * is torn down and rebuilt on every Turbo visit, so a preference flipped
     * mid-session is picked up on the next page anyway, and no media-query
     * listener has to survive (and leak past) that teardown.
     */
    shouldAutoplay: boolean
}

export function useAbilityMedia(championKey: string): AbilityMedia {
    const unavailable = reactive(new Set<AbilitySlot>())
    const url = (slot: AbilitySlot, ext: string): string =>
        `${CDN_BASE}/${championKey}/ability_${championKey}_${slot}1.${ext}`

    return {
        webm: (slot) => url(slot, 'webm'),
        mp4: (slot) => url(slot, 'mp4'),
        poster: (slot) => url(slot, 'jpg'),
        markUnavailable: (slot) => unavailable.add(slot),
        isAvailable: (slot) => !unavailable.has(slot),
        shouldAutoplay: !prefersReducedMotion(),
    }
}
