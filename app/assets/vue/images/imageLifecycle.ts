/**
 * Loading state of every `<img class="hx-img">` on the page, stamped as
 * `data-img-state` (loading | loaded | error) for foundation/images.css to draw
 * — the sweep while bytes are in flight, a neutral mark when they never land.
 *
 * One document-level capture listener per event (`load`/`error` do not bubble)
 * covers server-rendered and island-rendered images alike; the sweep catches
 * what finished before this module ran. Also owns the CSP-safe `data-img-fallback`
 * swap (the former inline `onerror`): a failure becomes a fresh load of the
 * fallback, once — the attribute is cleared first so a broken fallback cannot loop.
 */
export const IMAGE_CLASS = 'hx-img'
const STATE_ATTR = 'data-img-state'
const FALLBACK_ATTR = 'data-img-fallback'

type ImageState = 'loading' | 'loaded' | 'error'

export function installImageLifecycle(): void {
    document.addEventListener('load', onImageLoad, true)
    document.addEventListener('error', onImageError, true)
    document.addEventListener('DOMContentLoaded', () => markImages())
    document.addEventListener('turbo:load', () => markImages())
}

/**
 * Stamp the images under `root` that carry no state yet. Run after every
 * render that may insert images (page load, Turbo visit, island mount).
 */
export function markImages(root: ParentNode = document): void {
    for (const img of root.querySelectorAll<HTMLImageElement>(`img.${IMAGE_CLASS}:not([${STATE_ATTR}])`)) {
        setState(img, currentState(img))
    }
    // Failures that fired before the listener attached still get their fallback.
    for (const img of root.querySelectorAll<HTMLImageElement>(`img[${FALLBACK_ATTR}]`)) {
        if (img.complete && img.naturalWidth === 0) {
            swapToFallback(img)
        }
    }
}

function onImageLoad(event: Event): void {
    const img = event.target
    if (img instanceof HTMLImageElement && img.classList.contains(IMAGE_CLASS)) {
        setState(img, 'loaded')
    }
}

function onImageError(event: Event): void {
    const img = event.target
    if (!(img instanceof HTMLImageElement)) return
    if (img.hasAttribute(FALLBACK_ATTR)) {
        swapToFallback(img)
    } else if (img.classList.contains(IMAGE_CLASS)) {
        setState(img, 'error')
    }
}

/** A complete image with no intrinsic size is a failed one (or an empty src). */
function currentState(img: HTMLImageElement): ImageState {
    if (!img.complete) return 'loading'
    return img.naturalWidth > 0 ? 'loaded' : 'error'
}

function setState(img: HTMLImageElement, state: ImageState): void {
    img.setAttribute(STATE_ATTR, state)
}

function swapToFallback(img: HTMLImageElement): void {
    const fallback = img.getAttribute(FALLBACK_ATTR)
    if (fallback === null) return
    img.removeAttribute(FALLBACK_ATTR)
    if (img.classList.contains(IMAGE_CLASS)) setState(img, 'loading')
    img.src = fallback
}
