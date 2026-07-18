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
    'toaster': { load: () => import('./components/Toaster.vue') },
    'resource-loader': { load: () => import('./components/ResourceLoader.vue') },
    'chroma-strip': { load: () => import('./components/ChromaStrip.vue') },
    'skin-gallery': { load: () => import('./components/SkinGallery.vue') },
    'resource-filter': { load: () => import('./components/ResourceFilter.vue') },
    'load-time': { load: () => import('./components/LoadTimeBadge.vue') },
    'ability-showcase': { load: () => import('./components/AbilityShowcase.vue') },
    'stat-scaler': { load: () => import('./components/StatScaler.vue') },
    'favorite-picker': { load: () => import('./components/FavoritePicker.vue') },
    'skin-banner-picker': { load: () => import('./components/SkinBannerPicker.vue') },
    'build-editor': { load: () => import('./components/BuildEditor.vue') },
    'copy-link': { load: () => import('./components/CopyLink.vue') },
    'password-checklist': { load: () => import('./components/PasswordChecklist.vue') },
    'vote-score': { load: () => import('./components/VoteScore.vue') },
}

// Live islands, so Turbo navigations can tear them down instead of leaking a
// detached Vue tree (a playing <video> keeps its audio alive until GC).
const mountedIslands: { app: App; host: HTMLElement }[] = []

function mountIslands(root: ParentNode = document): void {
    root.querySelectorAll<HTMLElement>('[data-vue]:not([data-vue-mounted])').forEach(async (el) => {
        const name = el.dataset.vue
        const island = name ? registry[name] : undefined
        if (!island) {
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
        const app = createApp(component, props)
        app.mount(el)
        mountedIslands.push({ app, host: el })
    })
}

/**
 * Unmount every live island before Turbo caches or re-renders the page. This
 * stops in-flight media (audio bleeding across navigations) and clears the
 * mount flag so the cached snapshot re-mounts cleanly on a back/forward visit.
 */
function teardownIslands(): void {
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
