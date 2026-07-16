import '@hotwired/turbo'
import '../styles/app.css'
import 'primeicons/primeicons.css'

import { createApp, type Component } from 'vue'
import PrimeVue from 'primevue/config'
import Aura from '@primevue/themes/aura'

/**
 * Island registry: Twig renders a shell `<div data-vue="name" data-props="{...}">`,
 * and the matching component is lazily mounted into it. Keeps Symfony routing/SEO/i18n
 * while moving interactive pieces to Vue 3 + PrimeVue.
 */
const registry: Record<string, () => Promise<{ default: Component }>> = {
    'search-autocomplete': () => import('./components/SearchAutocomplete.vue'),
    'toaster': () => import('./components/Toaster.vue'),
    'number-nav': () => import('./components/NumberNav.vue'),
    'resource-loader': () => import('./components/ResourceLoader.vue'),
}

function mountIslands(root: ParentNode = document): void {
    root.querySelectorAll<HTMLElement>('[data-vue]:not([data-vue-mounted])').forEach(async (el) => {
        const name = el.dataset.vue
        const loader = name ? registry[name] : undefined
        if (!loader) {
            return
        }
        el.dataset.vueMounted = 'true'

        let props: Record<string, unknown> = {}
        try {
            props = el.dataset.props ? JSON.parse(el.dataset.props) : {}
        } catch {
            props = {}
        }

        const { default: component } = await loader()
        const app = createApp(component, props)
        app.use(PrimeVue, { theme: { preset: Aura, options: { darkModeSelector: 'system' } } })
        app.mount(el)
    })
}

document.addEventListener('DOMContentLoaded', () => mountIslands())
// The app uses Turbo Drive: re-scan for islands after each navigation.
document.addEventListener('turbo:load', () => mountIslands())
