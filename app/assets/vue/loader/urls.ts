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

/** Leading `/{version}/` path segment (dotted numeric) — mirrors VersionManager::VERSION_PATTERN. */
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

/** Only the home + list routes (and the build-editor warm token) ingest an image batch worth streaming. */
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

export function meta(name: string): string {
    return document.querySelector(`meta[name="${name}"]`)?.getAttribute('content') ?? ''
}

export function resolveVL(destUrl: string, override?: { version: string; lang: string }): { version: string; lang: string } {
    if (override) return override
    const u = new URL(destUrl, window.location.origin)
    return {
        // Path segment wins (canonical versioned URL), then query, then the session meta.
        version: versionFromPath(u.pathname) || u.searchParams.get('version') || meta('dd-version'),
        lang: u.searchParams.get('lang') || meta('dd-lang'),
    }
}

export function warmKey(destUrl: string, version: string, lang: string): string {
    const u = new URL(destUrl, window.location.origin)
    return [version, lang, u.pathname, u.searchParams.get('numpage') ?? '', u.searchParams.get('itemperpage') ?? ''].join('|')
}

export function prepareUrl(destUrl: string, version: string, lang: string): string {
    const u = new URL(destUrl, window.location.origin)
    const q = new URLSearchParams({ path: u.pathname, version, lang })
    const np = u.searchParams.get('numpage')
    const ip = u.searchParams.get('itemperpage')
    if (np) q.set('numpage', np)
    if (ip) q.set('itemperpage', ip)
    return `/api/loader/prepare?${q.toString()}`
}

/**
 * Destination after a patch/language switch. On a resource route the patch rides
 * in the path (`/{version}/…`, clean when it is the latest) so it survives — a
 * `?version=` query would be overridden by an existing path segment on a versioned
 * page. Elsewhere (home) the query drives the switch via the session fallback.
 */
export function destinationForSwitch(version: string, lang: string, latest: string): string {
    const u = new URL(window.location.href)
    const rest = pathWithoutVersion(u.pathname)

    const params = new URLSearchParams()
    if (lang) params.set('lang', lang)
    const np = u.searchParams.get('numpage'); if (np) params.set('numpage', np)
    const ip = u.searchParams.get('itemperpage'); if (ip) params.set('itemperpage', ip)

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

export function parse(ev: Event): Record<string, unknown> {
    try {
        return JSON.parse((ev as MessageEvent).data) as Record<string, unknown>
    } catch {
        return {}
    }
}
