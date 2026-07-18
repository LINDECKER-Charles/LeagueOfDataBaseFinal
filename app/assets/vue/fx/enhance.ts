/**
 * Progressive page effects, Turbo-safe: observers are torn down and rebuilt on
 * every visit so nothing leaks across navigations.
 *
 * - Reveal: sections tagged [data-reveal] rise into view once. The CSS only
 *   arms itself under `.js-reveal` on <html>, so content is never hidden
 *   without JavaScript.
 * - Scrollspy: a nav tagged [data-scrollspy] gets aria-current on the link
 *   whose target section currently crosses the reading band.
 */
const activeObservers: IntersectionObserver[] = []

const prefersReducedMotion = (): boolean =>
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

function initReveal(): void {
    document.documentElement.classList.add('js-reveal')

    const targets = Array.from(document.querySelectorAll<HTMLElement>('[data-reveal]:not(.reveal-in)'))
    if (targets.length === 0) {
        return
    }
    if (prefersReducedMotion()) {
        targets.forEach((el) => el.classList.add('reveal-in'))
        return
    }

    const observer = new IntersectionObserver(
        (entries, io) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('reveal-in')
                    io.unobserve(entry.target)
                }
            }
        },
        { rootMargin: '0px 0px -8% 0px', threshold: 0.05 },
    )
    targets.forEach((el) => observer.observe(el))
    activeObservers.push(observer)
}

function initScrollspy(): void {
    for (const nav of document.querySelectorAll<HTMLElement>('[data-scrollspy]')) {
        const links = Array.from(nav.querySelectorAll<HTMLAnchorElement>('a[href^="#"]'))
        const sections = links
            .map((link) => document.getElementById(link.hash.slice(1)))
            .filter((s): s is HTMLElement => s !== null)
        if (sections.length === 0) {
            continue
        }

        const linkFor = (id: string): HTMLAnchorElement | undefined =>
            links.find((l) => l.hash === `#${id}`)

        // Before any section crosses the reading band (page top), the first
        // chip stands for "start of the document".
        links[0]?.setAttribute('aria-current', 'true')

        const observer = new IntersectionObserver(
            (entries) => {
                const visible = entries.filter((e) => e.isIntersecting)
                if (visible.length === 0) {
                    return
                }
                links.forEach((l) => l.removeAttribute('aria-current'))
                linkFor(visible[0].target.id)?.setAttribute('aria-current', 'true')
            },
            // The "reading band": a section counts as current while it crosses
            // the upper-middle of the viewport.
            { rootMargin: '-35% 0px -55% 0px' },
        )
        sections.forEach((s) => observer.observe(s))
        activeObservers.push(observer)
    }
}

function enhance(): void {
    activeObservers.splice(0).forEach((io) => io.disconnect())
    initReveal()
    initScrollspy()
}

/** Close any open header switcher popover when interacting elsewhere. */
function closeSwitchersOutside(event: Event): void {
    for (const details of document.querySelectorAll<HTMLDetailsElement>('details.switcher[open]')) {
        if (event.target instanceof Node && !details.contains(event.target)) {
            details.open = false
        }
    }
}

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

export function installEnhancements(): void {
    document.addEventListener('DOMContentLoaded', enhance)
    document.addEventListener('turbo:load', enhance)
    document.addEventListener('click', closeSwitchersOutside)
    document.addEventListener('click', onSectionNavClick)
}
