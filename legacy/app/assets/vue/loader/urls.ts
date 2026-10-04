/**
 * Framework-free helpers for the navigation loader island. Pure functions (URL /
 * warm-key derivation, meta reads, SSE payload parsing) split out of the SFC so
 * they can be unit-tested and reasoned about apart from the Vue component.
 */

export interface Labels {
    champions: string
    items: string
    runes: string
    summoners: string
}

export type ResourceKey = keyof Labels
export type Phase = 'idle' | 'preparing' | 'loading' | 'done'

/** Server resource type ('runesReforged'…) → the label/icon key used here. */
export const TYPE_TO_KEY: Record<string, ResourceKey> = {
    champion: 'champions',
    item: 'items',
    summoner: 'summoners',
    runesReforged: 'runes',
}

/**
 * Leading `/{version}/` path segment (dotted numeric) — mirrors
 * VersionManager::VERSION_PATTERN.
 */
const VERSION_PREFIX = /^\/(\d+(?:\.\d+)+)(?=\/)/

/** Patch pinned in the URL path (`/15.14.1/champions`), else '' (clean/latest URL). */
export function versionFromPath(pathname: string): string {
    return pathname.match(VERSION_PREFIX)?.[1] ?? ''
}

/** Path with any leading `/{version}` segment stripped, so route matching is version-agnostic. */
export function pathWithoutVersion(pathname: string): string {
    return pathname.replace(VERSION_PREFIX, '') || '/'
}

/** Resource route the path renders, ignoring a version prefix. */
function isVersionedCapable(versionlessPath: string): boolean {
    return /^\/(champions|objects|runes|summoners)(\/|$)/.test(versionlessPath)
        || /^\/(champion|object|rune|summoner)\//.test(versionlessPath)
}

/**
 * Warm-only token (NOT a navigable route) the build editor hands to the loader
 * to pre-warm the patch it forges from. Mirrors `BUILD_WARM_PATH` in
 * {@see \App\Service\Client\PageContextResolver} — keep the two in sync.
 */
export const BUILD_WARM_PATH = '/builds/editor'

/**
 * Only the home + list routes (and the build-editor warm token) ingest an image
 * batch worth streaming.
 */
export function resourcesFor(pathname: string): ResourceKey[] {
    const p = (pathWithoutVersion(pathname).replace(/\/+$/, '') || '/').toLowerCase()
    if (p === '/') return ['champions', 'items', 'runes', 'summoners']
    if (p === '/champions') return ['champions']
    if (p === '/objects') return ['items']
    if (p === '/runes') return ['runes']
    if (p === '/summoners') return ['summoners']
    // Build editor loads champions + items + runes (no summoners) for one patch.
    if (p === BUILD_WARM_PATH) return ['champions', 'items', 'runes']
    return []
}

/** Content of a `<meta name="…">`, or '' when the page carries none. */
export function metaContent(name: string): string {
    return document.querySelector(`meta[name="${name}"]`)?.getAttribute('content') ?? ''
}

export function resolveVersionAndLang(
    destUrl: string,
    override?: { version: string; lang: string },
): { version: string; lang: string } {
    if (override) return override
    const target = new URL(destUrl, window.location.origin)
    return {
        // Path segment wins (canonical versioned URL), then query, then the session meta.
        version: versionFromPath(target.pathname)
            || target.searchParams.get('version')
            || metaContent('dd-version'),
        lang: target.searchParams.get('lang') || metaContent('dd-lang'),
    }
}

/**
 * Pagination params that take part in a warm's identity: two pages of the same
 * list warm different image batches, and a version switch must keep the reader
 * on their page. Declared once so the three functions below cannot drift.
 */
const WARM_QUERY_PARAMS = ['numpage', 'itemperpage'] as const

/** Copy the warm-identity params of `from` into a query being built. */
function carryWarmParams(from: URL, into: URLSearchParams): void {
    for (const name of WARM_QUERY_PARAMS) {
        const value = from.searchParams.get(name)
        if (value) into.set(name, value)
    }
}

export function warmKey(destUrl: string, version: string, lang: string): string {
    const target = new URL(destUrl, window.location.origin)
    const pagination = WARM_QUERY_PARAMS.map((name) => target.searchParams.get(name) ?? '')
    return [version, lang, target.pathname, ...pagination].join('|')
}

export function prepareUrl(destUrl: string, version: string, lang: string): string {
    const target = new URL(destUrl, window.location.origin)
    const query = new URLSearchParams({ path: target.pathname, version, lang })
    carryWarmParams(target, query)
    return `/api/loader/prepare?${query.toString()}`
}

/**
 * Destination after a patch/language switch. On a resource route the patch rides
 * in the path (`/{version}/…`, clean when it is the latest) so it survives — a
 * `?version=` query would be overridden by an existing path segment on a versioned
 * page. Elsewhere (home) the query drives the switch via the session fallback.
 */
export function destinationForSwitch(version: string, lang: string, latest: string): string {
    const current = new URL(window.location.href)
    const rest = pathWithoutVersion(current.pathname)

    // Everything else in the query (the list filters, notably) survives the
    // switch; only the selection itself is rewritten.
    const params = new URLSearchParams()
    for (const [name, value] of current.searchParams) {
        if (name !== 'version' && name !== 'lang') params.set(name, value)
    }
    if (lang) params.set('lang', lang)
    carryWarmParams(current, params)

    let base = rest
    if (isVersionedCapable(rest)) {
        if (version && version !== latest) base = `/${version}${rest}`
    } else if (version) {
        params.set('version', version)
    }

    const qs = params.toString()
    return qs ? `${base}?${qs}` : base
}

export function turbo(): { visit?: (url: string) => void } | undefined {
    return (window as unknown as { Turbo?: { visit?: (url: string) => void } }).Turbo
}

/** JSON body of an SSE message; a malformed frame degrades to an empty payload. */
export function parseStreamPayload(ev: Event): Record<string, unknown> {
    try {
        return JSON.parse((ev as MessageEvent).data) as Record<string, unknown>
    } catch {
        return {}
    }
}
