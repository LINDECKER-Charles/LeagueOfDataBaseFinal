import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import CatalogState from './CatalogState.vue'
import type { UiLabels } from '../editor/editorLabels'

const UI: UiLabels = {
    loading: 'Chargement',
    error: 'Erreur',
    retry: 'Réessayer',
    ghost: 'Absent du patch',
    ghostMode: 'Exclu du mode',
    counter: '%count% / %max%',
}

function mountState(state: { isLoading: boolean; hasError: boolean }) {
    return mount(CatalogState, {
        props: { ...state, ui: UI },
        slots: { default: '<p class="ready">catalog</p>' },
    })
}

describe('CatalogState', () => {
    it('announces the loading hint politely and hides the content', () => {
        const wrapper = mountState({ isLoading: true, hasError: false })
        expect(wrapper.get('[role="status"]').text()).toBe(UI.loading)
        expect(wrapper.find('.ready').exists()).toBe(false)
    })

    it('offers a retry that reaches the caller when the catalog failed', async () => {
        const wrapper = mountState({ isLoading: false, hasError: true })
        expect(wrapper.find('.ready').exists()).toBe(false)

        await wrapper.get('button').trigger('click')
        expect(wrapper.emitted('retry')).toHaveLength(1)
    })

    it('renders the caller content once the catalog is ready', () => {
        const wrapper = mountState({ isLoading: false, hasError: false })
        expect(wrapper.get('.ready').text()).toBe('catalog')
        expect(wrapper.find('button').exists()).toBe(false)
    })

    it('keeps loading in front of an error (a retry is already in flight)', () => {
        const wrapper = mountState({ isLoading: true, hasError: true })
        expect(wrapper.get('[role="status"]').text()).toBe(UI.loading)
    })
})
