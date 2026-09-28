import { prefersReducedMotion } from './reducedMotion'

/**
 * Own the in-page scroll for the [data-scrollspy] section nav. Turbo Drive's
 * same-page anchor shortcut compares the link's request URL against its
 * last-rendered location; on these detail pages the URL carries a ?version&lang
 * query, the comparison misses, and Turbo runs a full Drive *visit* — the anchor
 * "reloads" instead of scrolling. Handling the click here makes it behave like a
 * plain fragment link (no refetch), with a smooth, reduced-motion-aware scroll
 * and a deep-linkable URL. Delegated at document level so it survives Turbo body
 * swaps without per-visit rebinding.
 */
export function installSectionNav(): void {
    document.addEventListener('click', onSectionNavClick)
}

function onSectionNavClick(event: MouseEvent): void {
    if (event.defaultPrevented || event.button !== 0
        || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return
    }
    const link = (event.target as Element | null)
        ?.closest<HTMLAnchorElement>('[data-scrollspy] a[href^="#"]')
    const target = link && document.getElementById(link.hash.slice(1))
    if (!target) {
        return
    }
    event.preventDefault()
    target.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' })
    history.pushState(history.state, '', link.hash)
}
