import { prefersReducedMotion } from './reducedMotion'

/**
 * Scroll reveal: sections tagged [data-reveal] rise into view once. The CSS only
 * arms itself under `.js-reveal` on <html>, so content is never hidden without
 * JavaScript. Reduced motion jumps straight to the revealed state.
 */
const REVEAL_BAND = { rootMargin: '0px 0px -8% 0px', threshold: 0.05 }

let observer: IntersectionObserver | null = null

export function installReveal(): void {
    document.addEventListener('DOMContentLoaded', revealSections)
    document.addEventListener('turbo:load', revealSections)
}

/** Rebuilt on every visit — the previous page's observer holds detached nodes. */
function revealSections(): void {
    observer?.disconnect()
    observer = null
    document.documentElement.classList.add('js-reveal')

    const targets = Array.from(
        document.querySelectorAll<HTMLElement>('[data-reveal]:not(.reveal-in)'),
    )
    if (targets.length === 0) {
        return
    }
    if (prefersReducedMotion()) {
        targets.forEach((el) => el.classList.add('reveal-in'))
        return
    }

    observer = new IntersectionObserver((entries, io) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('reveal-in')
                io.unobserve(entry.target)
            }
        }
    }, REVEAL_BAND)
    targets.forEach((el) => observer?.observe(el))
}
