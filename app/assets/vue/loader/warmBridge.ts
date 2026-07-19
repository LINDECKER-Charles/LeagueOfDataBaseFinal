import { BUILD_WARM_PATH } from './urls'

/**
 * Cross-island seam for the navigation loader. An island that loads a resource
 * catalog *in place* — e.g. the build editor switching to a patch whose images
 * aren't warm yet — asks the global {@link ResourceLoader} to pre-warm that
 * patch with the same SSE progress overlay used for navigation, then resumes
 * once the stream completes (so the reloaded pickers land on real icons, not
 * placeholders).
 *
 * The emitted `path` is matched server-side by
 * `PageContextResolver::loaderSteps()`; {@link BUILD_WARM_PATH} is the token for
 * the champion/item/rune full set. The loader island claims a request by calling
 * `preventDefault()`; if none is mounted the promise resolves at once so the
 * caller degrades to its own fetch with no overlay.
 */

export { BUILD_WARM_PATH }

export const WARM_REQUEST_EVENT = 'lodb:loader-warm'

export interface WarmRequestDetail {
    version: string
    lang: string
    /** Warm token understood by `loaderSteps()` (e.g. {@link BUILD_WARM_PATH}). */
    path: string
    /** Called by the loader island once the stream has drained (or errored out). */
    resolve: () => void
}

/**
 * Ask the loader island to warm `(version, lang)` for `path`, resolving when it
 * finishes. Resolves immediately when no loader island handles the request.
 */
export function requestWarm(version: string, lang: string, path: string): Promise<void> {
    return new Promise((resolve) => {
        const detail: WarmRequestDetail = { version, lang, path, resolve }
        // dispatchEvent → false when a handler called preventDefault (claimed it).
        const claimed = !document.dispatchEvent(
            new CustomEvent<WarmRequestDetail>(WARM_REQUEST_EVENT, { detail, cancelable: true }),
        )
        if (!claimed) resolve()
    })
}
