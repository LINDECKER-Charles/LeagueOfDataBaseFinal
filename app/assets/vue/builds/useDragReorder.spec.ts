import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { useDragReorder, type DragReorder, type DragReorderOptions } from './useDragReorder'

/**
 * The composable is a pure state machine over native drag events: it never
 * touches the DOM besides the document Escape listener, so a stubbed DragEvent
 * (jsdom has none) exercises the full contract.
 */
type Source = { kind: string; index: number }
type Target = { insert: number }

function dragEvent(): DragEvent {
    return {
        preventDefault: vi.fn(),
        dataTransfer: { setData: vi.fn(), effectAllowed: '', dropEffect: '' },
    } as unknown as DragEvent
}

/** Mounts a host so onBeforeUnmount registers; returns the composable + host. */
function harness(options: DragReorderOptions<Source, Target>) {
    let drag!: DragReorder<Source, Target>
    const wrapper = mount(
        defineComponent({
            setup() {
                drag = useDragReorder<Source, Target>(options)
                return () => null
            },
        }),
    )
    return { drag, wrapper }
}

describe('useDragReorder', () => {
    it('commits source and target on drop, then clears', () => {
        const onCommit = vi.fn()
        const { drag } = harness({ onCommit })

        const start = dragEvent()
        drag.start({ kind: 'step', index: 1 }, start)
        expect(drag.isDragging.value).toBe(true)
        expect(start.dataTransfer?.setData).toHaveBeenCalled()

        const over = dragEvent()
        drag.over({ insert: 3 }, over)
        expect(over.preventDefault).toHaveBeenCalled()
        expect(drag.target.value).toEqual({ insert: 3 })

        drag.drop(dragEvent())
        expect(onCommit).toHaveBeenCalledOnce()
        expect(onCommit).toHaveBeenCalledWith({ kind: 'step', index: 1 }, { insert: 3 })
        expect(drag.isDragging.value).toBe(false)
        expect(drag.target.value).toBeNull()
    })

    it('ignores dragover before any drag started', () => {
        const { drag } = harness({ onCommit: vi.fn() })
        const event = dragEvent()

        drag.over({ insert: 0 }, event)

        expect(event.preventDefault).not.toHaveBeenCalled()
        expect(drag.target.value).toBeNull()
    })

    it('drop without a target commits nothing', () => {
        const onCommit = vi.fn()
        const { drag } = harness({ onCommit })

        drag.start({ kind: 'item', index: 0 }, dragEvent())
        drag.drop(dragEvent())

        expect(onCommit).not.toHaveBeenCalled()
        expect(drag.isDragging.value).toBe(false)
    })

    it('dragend after a release outside any zone cancels', () => {
        const onCommit = vi.fn()
        const onCancel = vi.fn()
        const { drag } = harness({ onCommit, onCancel })

        drag.start({ kind: 'item', index: 0 }, dragEvent())
        drag.over({ insert: 1 }, dragEvent())
        drag.end()

        expect(onCancel).toHaveBeenCalledOnce()
        expect(onCommit).not.toHaveBeenCalled()
        expect(drag.isDragging.value).toBe(false)
    })

    it('dragend after a successful drop stays silent', () => {
        const onCancel = vi.fn()
        const { drag } = harness({ onCommit: vi.fn(), onCancel })

        drag.start({ kind: 'item', index: 0 }, dragEvent())
        drag.over({ insert: 1 }, dragEvent())
        drag.drop(dragEvent())
        drag.end()

        expect(onCancel).not.toHaveBeenCalled()
    })

    it('Escape cancels the drag through the document listener', () => {
        const onCancel = vi.fn()
        const { drag } = harness({ onCommit: vi.fn(), onCancel })

        drag.start({ kind: 'step', index: 0 }, dragEvent())
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

        expect(onCancel).toHaveBeenCalledOnce()
        expect(drag.isDragging.value).toBe(false)

        // Listener is gone: a second Escape does nothing.
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
        expect(onCancel).toHaveBeenCalledOnce()
    })

    it('leave clears only the matching target', () => {
        const { drag } = harness({ onCommit: vi.fn() })

        drag.start({ kind: 'step', index: 0 }, dragEvent())
        drag.over({ insert: 2 }, dragEvent())
        drag.leave({ insert: 1 })
        expect(drag.target.value).toEqual({ insert: 2 })
        drag.leave({ insert: 2 })
        expect(drag.target.value).toBeNull()
    })

    it('removes the Escape listener on unmount', () => {
        const onCancel = vi.fn()
        const { drag, wrapper } = harness({ onCommit: vi.fn(), onCancel })

        drag.start({ kind: 'step', index: 0 }, dragEvent())
        wrapper.unmount()
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

        expect(onCancel).not.toHaveBeenCalled()
    })
})
