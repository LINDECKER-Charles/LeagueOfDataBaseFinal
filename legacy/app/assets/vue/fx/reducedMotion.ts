const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)'

/**
 * One-shot read of the OS motion preference. Deliberately a function, not a
 * reactive value: every caller reads it at the moment it decides how to move,
 * which keeps the answer honest without a listener to leak across Turbo visits.
 */
export function prefersReducedMotion(): boolean {
    return window.matchMedia(REDUCED_MOTION_QUERY).matches
}
