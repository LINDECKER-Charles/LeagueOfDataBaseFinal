/**
 * Rewrite the current URL in place (no navigation, no history entry) while
 * keeping Turbo Drive's bookkeeping in step. Turbo keys its snapshot cache by
 * the location IT last rendered: a bare `history.replaceState` would leave that
 * at the unfiltered URL, and every Back to the filtered list would miss the
 * cache and refetch the whole page. The Turbo internals touched here are
 * feature-detected — without them the plain rewrite still applies.
 */
interface TurboHistory {
    replace: (location: URL, restorationIdentifier?: string) => void
    restorationIdentifier?: string
}

interface TurboSession {
    history?: TurboHistory
    view?: { lastRenderedLocation?: URL }
}

export function syncLocation(url: string): void {
    const location = new URL(url, window.location.origin)
    const session = turboSession()
    const history = session?.history
    if (typeof history?.replace === 'function') {
        history.replace(location, history.restorationIdentifier)
    } else {
        window.history.replaceState(window.history.state, '', location)
    }
    if (session?.view) {
        session.view.lastRenderedLocation = location
    }
}

function turboSession(): TurboSession | undefined {
    const turbo = (window as unknown as { Turbo?: { session?: TurboSession } }).Turbo
    return turbo?.session
}
