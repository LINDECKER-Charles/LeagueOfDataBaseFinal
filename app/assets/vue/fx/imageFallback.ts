/**
 * CSP-safe image fallback, replacing the former inline `onerror`. The swap runs
 * once — the attribute is cleared first — so a broken fallback URL cannot loop.
 */
const IMG_FALLBACK_ATTR = 'data-img-fallback'

export function installImageFallback(): void {
    // `error` does not bubble, so image failures are caught at document capture.
    document.addEventListener('error', onImageError, true)
    document.addEventListener('DOMContentLoaded', sweepFailedImages)
    document.addEventListener('turbo:load', sweepFailedImages)
}

function onImageError(event: Event): void {
    const img = event.target
    if (img instanceof HTMLImageElement && img.hasAttribute(IMG_FALLBACK_ATTR)) {
        swapToFallback(img)
    }
}

/**
 * Images that failed before this deferred module ran: the old inline handler
 * fired during parse, a document listener attaches later. Re-check once per
 * render so the first paint's broken art still swaps to its fallback.
 */
function sweepFailedImages(): void {
    for (const img of document.querySelectorAll<HTMLImageElement>(`img[${IMG_FALLBACK_ATTR}]`)) {
        if (img.complete && img.naturalWidth === 0) {
            swapToFallback(img)
        }
    }
}

function swapToFallback(img: HTMLImageElement): void {
    const fallback = img.getAttribute(IMG_FALLBACK_ATTR)
    if (fallback === null) {
        return
    }
    img.removeAttribute(IMG_FALLBACK_ATTR)
    img.src = fallback
}
