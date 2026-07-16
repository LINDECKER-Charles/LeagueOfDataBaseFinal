import { flushPromises, mount } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import Toaster from './Toaster.vue'

// Toasts are appended in onMounted, so flush the resulting reactive render
// before asserting.
describe('Toaster', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('renders one toast per flash message', async () => {
    const w = mount(Toaster, {
      props: { messages: [
        { type: 'success', text: 'Saved' },
        { type: 'error', text: 'Nope' },
      ] },
    })
    await flushPromises()
    expect(w.findAll('[role="alert"]')).toHaveLength(2)
    expect(w.text()).toContain('Saved')
    expect(w.text()).toContain('Nope')
  })

  it('renders nothing when there are no messages', async () => {
    const w = mount(Toaster, { props: { messages: [] } })
    await flushPromises()
    expect(w.findAll('[role="alert"]')).toHaveLength(0)
  })

  it('opens a modal with the message when a toast is clicked', async () => {
    const w = mount(Toaster, {
      props: { messages: [{ type: 'info', text: 'Hello' }] },
      attachTo: document.body,
    })
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    await w.get('[role="alert"]').trigger('click')
    const dialog = document.body.querySelector('[role="dialog"]')
    expect(dialog).not.toBeNull()
    expect(dialog?.textContent).toContain('Hello')
    w.unmount()
  })
})
