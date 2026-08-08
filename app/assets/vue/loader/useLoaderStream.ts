import { onBeforeUnmount, onMounted } from 'vue'
import { LoaderController } from './LoaderController'
import { bindLoaderHooks } from './loaderHooks'
import { createLoaderState, type LoaderState } from './loaderState'

/**
 * SSE "gate-then-visit" navigation loader, extracted from
 * {@link ResourceLoader.vue} so the orchestration is testable and readable apart
 * from the presentation. The reactive surface lives in {@link createLoaderState},
 * the state machine (EventSource lifecycle, inactivity watchdog, held-back
 * visit) in {@link LoaderController} and the Turbo wiring in
 * {@link bindLoaderHooks}; this composable binds the three to the island's
 * lifecycle and hands the template what it renders.
 */
export function useLoaderStream(): LoaderState {
    const state = createLoaderState()
    const controller = new LoaderController(state)
    let unbind: (() => void) | null = null

    onMounted(() => { unbind = bindLoaderHooks(controller) })
    onBeforeUnmount(() => {
        unbind?.()
        controller.stop()
    })

    return state
}
