/**
 * Scrollspy: a nav tagged [data-scrollspy] gets aria-current on the link whose
 * target section currently crosses the reading band.
 */

/** A section counts as current while it crosses the upper-middle of the viewport. */
const READING_BAND = { rootMargin: '-35% 0px -55% 0px' }

const observers: IntersectionObserver[] = []

export function installScrollspy(): void {
    document.addEventListener('DOMContentLoaded', trackSections)
    document.addEventListener('turbo:load', trackSections)
}

/** Rebuilt on every visit — the previous page's observers hold detached nodes. */
function trackSections(): void {
    observers.splice(0).forEach((io) => io.disconnect())

    for (const nav of document.querySelectorAll<HTMLElement>('[data-scrollspy]')) {
        const links = Array.from(nav.querySelectorAll<HTMLAnchorElement>('a[href^="#"]'))
        const sections = links
            .map((link) => document.getElementById(link.hash.slice(1)))
            .filter((s): s is HTMLElement => s !== null)
        if (sections.length === 0) {
            continue
        }
        // Before any section crosses the reading band (page top), the first chip
        // stands for "start of the document".
        links[0]?.setAttribute('aria-current', 'true')
        observers.push(observeSections(sections, links))
    }
}

function observeSections(
    sections: HTMLElement[],
    links: HTMLAnchorElement[],
): IntersectionObserver {
    const observer = new IntersectionObserver((entries) => {
        const visible = entries.filter((e) => e.isIntersecting)
        if (visible.length === 0) {
            return
        }
        links.forEach((l) => l.removeAttribute('aria-current'))
        links
            .find((l) => l.hash === `#${visible[0].target.id}`)
            ?.setAttribute('aria-current', 'true')
    }, READING_BAND)
    sections.forEach((s) => observer.observe(s))

    return observer
}
