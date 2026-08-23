/**
 * Theme toggle — swaps the visual identity the design system paints with.
 *
 * This module never causes the first paint: the server already rendered
 * `data-theme` on <html> from the `lod_theme` cookie (App\Service\Client\
 * ThemeResolver), which is what keeps the wrong identity from flashing. An
 * inline bootstrap script could not do that job anyway — the CSP ships
 * `script-src 'self'` with neither 'unsafe-inline' nor a nonce.
 *
 * Turbo Drive never replaces <html> (its renderer only syncs `lang` and `dir`),
 * so the attribute survives navigation on its own. The re-assertion on
 * `turbo:load` is therefore not belt-and-braces: the service worker replays
 * cached pages without honouring the cookie, so a page restored offline — or
 * after the 4s network timeout — can carry a stale identity, and a second tab
 * may have switched themes in the meantime.
 *
 * <meta name="theme-color"> is the mirror case: Turbo DOES re-merge it from each
 * response, so it follows the server. It only needs patching at the instant of
 * the swap, before any navigation republishes it.
 */
const COOKIE = 'lod_theme'
const COOKIE_MAX_AGE_SECONDS = 31_536_000

/** Mirrors App\Service\Client\Theme — the server validates against the same set. */
const THEMES = ['hextech', 'zaun'] as const
type ThemeName = (typeof THEMES)[number]

const DEFAULT_THEME: ThemeName = 'hextech'

export function installTheme(): void {
    document.addEventListener('click', onOptionClick)
    document.addEventListener('turbo:load', () => apply(readCookie()))
}

function onOptionClick(event: Event): void {
    if (!(event.target instanceof Element)) {
        return
    }
    const option = event.target.closest<HTMLElement>('[data-theme-option]')
    const name = option?.dataset.themeOption

    if (!isTheme(name)) {
        return
    }
    writeCookie(name)
    apply(name)
}

function apply(name: ThemeName): void {
    document.documentElement.dataset.theme = name

    document.querySelectorAll<HTMLElement>('[data-theme-option]').forEach((option) => {
        const selected = option.dataset.themeOption === name
        option.setAttribute('aria-pressed', String(selected))
        if (selected && option.dataset.themeChrome) {
            paintBrowserChrome(option.dataset.themeChrome)
        }
    })
}

/**
 * The chrome colour is carried by the markup rather than duplicated here, so
 * Theme::browserColor() stays the single source of truth for it.
 */
function paintBrowserChrome(color: string): void {
    document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')?.setAttribute(
        'content',
        color,
    )
}

function readCookie(): ThemeName {
    const raw = document.cookie
        .split('; ')
        .find((entry) => entry.startsWith(`${COOKIE}=`))
        ?.slice(COOKIE.length + 1)

    return isTheme(raw) ? raw : DEFAULT_THEME
}

function writeCookie(name: ThemeName): void {
    // Firefox rejects a Secure cookie written from an insecure origin, which would
    // silently break the toggle on a plain-http local stack.
    const secure = location.protocol === 'https:' ? '; secure' : ''

    document.cookie =
        `${COOKIE}=${name}; path=/; max-age=${COOKIE_MAX_AGE_SECONDS}; samesite=lax${secure}`
}

function isTheme(value: string | undefined): value is ThemeName {
    return THEMES.includes(value as ThemeName)
}
