import '@hotwired/turbo'
import '../styles/app.css'

import { createApp, type App, type Component } from 'vue'
import { installEnhancements } from './fx/enhance'
import { setupProfileForm } from './profile/profileForm'

/**
 * Island registry: Twig renders a shell `<div data-vue="name" data-props="{...}">`,
 * and the matching component is lazily (code-split) mounted into it — keeping Symfony
 * routing/SEO/i18n while moving interactive pieces to Vue 3.
 */
interface Island {
    load: () => Promise<{ default: Component }>
}

const registry: Record<string, Island> = {
    'toaster': { load: () => import('./components/ui/Toaster.vue') },
    'resource-loader': { load: () => import('./components/catalog/ResourceLoader.vue') },
    'chroma-strip': { load: () => import('./components/codex/ChromaStrip.vue') },
    'skin-gallery': { load: () => import('./components/codex/SkinGallery.vue') },
    'resource-filter': { load: () => import('./components/catalog/ResourceFilter.vue') },
    'load-time': { load: () => import('./components/catalog/LoadTimeBadge.vue') },
    'ability-showcase': { load: () => import('./components/codex/AbilityShowcase.vue') },
    'stat-scaler': { load: () => import('./components/codex/StatScaler.vue') },
    'favorite-picker': { load: () => import('./components/account/FavoritePicker.vue') },
    'skin-banner-picker': { load: () => import('./components/account/SkinBannerPicker.vue') },
    'build-editor': { load: () => import('./components/builds/BuildEditor.vue') },
    'copy-link': { load: () => import('./components/ui/CopyLink.vue') },
    'password-checklist': { load: () => import('./components/account/PasswordChecklist.vue') },
    'vote-score': { load: () => import('./components/community/VoteScore.vue') },
}

// Live islands, so Turbo navigations can tear them down instead of leaking a
// detached Vue tree (a playing <video> keeps its audio alive until GC).
const mountedIslands: { app: App; host: HTMLElement }[] = []

// Shells flagged `data-vue-lazy` sit below the fold behind a server-rendered
// fallback or skeleton: their chunk waits until they approach the viewport, so
// it never competes with the critical path.
const LAZY_ROOT_MARGIN = '250px'
let lazyObserver: IntersectionObserver | null = null

function mountIslands(root: ParentNode = document): void {
    root.querySelectorAll<HTMLElement>('[data-vue]:not([data-vue-mounted])').forEach((el) => {
        if (el.dataset.vueLazy !== undefined && 'IntersectionObserver' in window) {
            observeIsland(el)

            return
        }
        void mountIsland(el)
    })
}

function observeIsland(el: HTMLElement): void {
    lazyObserver ??= new IntersectionObserver(
        (entries, observer) => {
            entries.filter((entry) => entry.isIntersecting).forEach((entry) => {
                observer.unobserve(entry.target)
                void mountIsland(entry.target as HTMLElement)
            })
        },
        { rootMargin: LAZY_ROOT_MARGIN },
    )
    lazyObserver.observe(el)
}

async function mountIsland(el: HTMLElement): Promise<void> {
    const name = el.dataset.vue
    const island = name ? registry[name] : undefined
    if (!island || el.dataset.vueMounted) {
        return
    }
    el.dataset.vueMounted = 'true'

    let props: Record<string, unknown> = {}
    try {
        props = el.dataset.props ? JSON.parse(el.dataset.props) : {}
    } catch {
        props = {}
    }

    const { default: component } = await island.load()
    // The chunk may resolve after a Turbo visit swapped this shell away.
    if (!el.isConnected) {
        return
    }
    // Mounting clears the shell, so the server-rendered skeleton/fallback inside
    // it disappears on its own — no teardown to wire.
    const app = createApp(component, props)
    app.mount(el)
    mountedIslands.push({ app, host: el })
}

/**
 * Unmount every live island before Turbo caches or re-renders the page. This
 * stops in-flight media (audio bleeding across navigations) and clears the
 * mount flag so the cached snapshot re-mounts cleanly on a back/forward visit.
 */
function teardownIslands(): void {
    // The observer holds references to shells the visit is about to detach.
    lazyObserver?.disconnect()
    lazyObserver = null
    while (mountedIslands.length > 0) {
        const { app, host } = mountedIslands.pop()!
        app.unmount()
        delete host.dataset.vueMounted
    }
}

function enhancePage(root: ParentNode = document): void {
    mountIslands(root)
    setupProfileForm(root)
}

document.addEventListener('DOMContentLoaded', () => enhancePage())
// The app uses Turbo Drive: re-scan for islands after each navigation, and tear
// the previous page's islands down before caching/rendering so nothing (audio
// especially) leaks across visits. before-cache covers cacheable pages;
// before-render is the fallback for non-cached visits.
document.addEventListener('turbo:load', () => enhancePage())
document.addEventListener('turbo:before-cache', teardownIslands)
document.addEventListener('turbo:before-render', teardownIslands)

// Scroll-reveal + section-nav scrollspy (Turbo-safe, reduced-motion aware).
installEnhancements()

// PWA: offline resilience + installability. Production builds only — the Vite
// dev server has no /sw.js and a dev-registered worker would shadow it.
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js').catch(() => {
            // Registration is progressive enhancement; the site works without it.
        })
    })
}
