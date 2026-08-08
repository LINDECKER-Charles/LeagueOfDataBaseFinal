/**
 * Footer contact form modal. A native <dialog> needs JS to open as a modal;
 * delegated at document level so it survives Turbo body swaps (the footer, and
 * thus the trigger + dialog, re-render every visit). Esc-to-close is native.
 */
export function installContactDialog(): void {
    document.addEventListener('click', onContactDialogClick)
}

function onContactDialogClick(event: MouseEvent): void {
    const target = event.target as Element | null
    if (target?.closest('[data-contact-open]')) {
        const dialog = document.getElementById('contact-dialog')
        if (dialog instanceof HTMLDialogElement && !dialog.open) {
            dialog.showModal()
        }
        return
    }
    if (target?.closest('[data-contact-close]')) {
        target.closest('dialog')?.close()
        return
    }
    // A click on the dialog element itself (not its inner form) is the backdrop.
    if (target instanceof HTMLDialogElement) {
        target.close()
    }
}
