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

/** Only the home + list routes ingest an image batch worth streaming. */
export function resourcesFor(pathname: string): ResourceKey[] {
    const p = (pathname.replace(/\/+$/, '') || '/').toLowerCase()
    if (p === '/home') return ['champions', 'items', 'runes', 'summoners']
    if (p === '/champions') return ['champions']
    if (p === '/objects') return ['items']
    if (p === '/runes') return ['runes']
    if (p === '/summoners') return ['summoners']
    return []
}

export function meta(name: string): string {
    return document.querySelector(`meta[name="${name}"]`)?.getAttribute('content') ?? ''
}

export function resolveVL(destUrl: string, override?: { version: string; lang: string }): { version: string; lang: string } {
    if (override) return override
    const u = new URL(destUrl, window.location.origin)
    return {
        version: u.searchParams.get('version') || meta('dd-version'),
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

export function destinationForSwitch(version: string, lang: string): string {
    const u = new URL(window.location.href)
    u.searchParams.set('version', version)
    u.searchParams.set('lang', lang)
    return `${u.pathname}?${u.searchParams.toString()}`
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
