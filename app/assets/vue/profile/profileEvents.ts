/**
 * Seam between the /profile islands and the plain <form> that hosts them.
 * The islands own hidden inputs, so a pick produces no native `change`: they
 * bubble this event instead and the form's auto-save enhancement listens for it.
 * Shared constant — a typo on either side would silently kill auto-save.
 */
export const PROFILE_CHANGED_EVENT = 'profile:changed'

/** Bubble a profile change from an island root up to the hosting <form>. */
export function notifyProfileChanged(root: HTMLElement | null): void {
    root?.dispatchEvent(new CustomEvent(PROFILE_CHANGED_EVENT, { bubbles: true }))
}
