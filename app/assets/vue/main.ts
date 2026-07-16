import '@hotwired/turbo'
import '../styles/app.css'

import { createApp, type App, type Component } from 'vue'

/**
 * Island registry: Twig renders a shell `<div data-vue="name" data-props="{...}">`,
 * and the matching component is lazily mounted into it. Keeps Symfony routing/SEO/i18n
 * while moving interactive pieces to Vue 3 + PrimeVue.
 *
 * PrimeVue (config + Aura theme, ~heavy) is only pulled in by islands that declare
 * `setup: usePrimeVue`, so pages without a PrimeVue widget never download it — it is
 * code-split out of the main entry chunk instead of loading on every page.
 */
interface Island {
    load: () => Promise<{ default: Component }>
    setup?: (app: App) => Promise<void>
}

async function usePrimeVue(app: App): Promise<void> {
    const [{ default: PrimeVue }, { default: Aura }] = await Promise.all([
        import('primevue/config'),
        import('@primevue/themes/aura'),
    ])
    app.use(PrimeVue, { theme: { preset: Aura, options: { darkModeSelector: 'system' } } })
}

const registry: Record<string, Island> = {
    'search-autocomplete': { load: () => import('./components/SearchAutocomplete.vue'), setup: usePrimeVue },
    'toaster': { load: () => import('./components/Toaster.vue') },
    'number-nav': { load: () => import('./components/NumberNav.vue') },
    'resource-loader': { load: () => import('./components/ResourceLoader.vue') },
    'chroma-strip': { load: () => import('./components/ChromaStrip.vue') },
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
        const app = createApp(component, props)
        if (island.setup) {
            await island.setup(app)
        }
        app.mount(el)
    })
}

document.addEventListener('DOMContentLoaded', () => mountIslands())
// The app uses Turbo Drive: re-scan for islands after each navigation.
document.addEventListener('turbo:load', () => mountIslands())
