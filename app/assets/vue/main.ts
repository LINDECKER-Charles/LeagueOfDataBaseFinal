import '@hotwired/turbo'
import '../styles/app.css'

import { createApp, type Component } from 'vue'

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
