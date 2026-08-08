/**
 * CSP-safe submit confirmation, replacing the former inline `onsubmit`. Captured
 * so it runs ahead of Turbo's own submit handling; a declined confirm stops the
 * native submit AND Turbo (stopImmediatePropagation halts the bubble phase).
 */
const CONFIRM_ATTR = 'data-confirm'

export function installConfirmSubmit(): void {
    document.addEventListener('submit', onConfirmSubmit, true)
}

function onConfirmSubmit(event: Event): void {
    const form = event.target
    if (!(form instanceof HTMLFormElement)) {
        return
    }
    const message = form.getAttribute(CONFIRM_ATTR)
    if (message !== null && !window.confirm(message)) {
        event.preventDefault()
        event.stopImmediatePropagation()
    }
}
