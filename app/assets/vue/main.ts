import '@hotwired/turbo'
import '../styles/app.css'

import { createApp, type Component } from 'vue'
import { installEnhancements } from './fx/enhance'

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
    'build-editor': { load: () => import('./components/BuildEditor.vue') },
    'copy-link': { load: () => import('./components/CopyLink.vue') },
}

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
        createApp(component, props).mount(el)
    })
}

document.addEventListener('DOMContentLoaded', () => mountIslands())
// The app uses Turbo Drive: re-scan for islands after each navigation.
document.addEventListener('turbo:load', () => mountIslands())

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
