import { onBeforeUnmount, onMounted } from 'vue'

/**
 * `/` jumps to the search field, the way code hosts and docs sites do. The
 * key is left alone while the reader types elsewhere, and among the candidate
 * fields (a rail and a mobile bar that never show together) the first one
 * actually laid out takes the focus.
 */
export const SEARCH_SHORTCUT_KEY = '/'
const EDITABLE_TAGS = new Set(['INPUT', 'TEXTAREA', 'SELECT'])

export function useSearchShortcut(candidates: () => readonly (HTMLInputElement | null)[]): void {
    const onKeydown = (event: KeyboardEvent): void => {
        if (event.key !== SEARCH_SHORTCUT_KEY || event.ctrlKey || event.metaKey || event.altKey) return
        if (isTyping(document.activeElement)) return
        const target = candidates().find((input) => input !== null && isLaidOut(input))
        if (!target) return
        event.preventDefault()
        target.focus()
        target.select()
    }

    onMounted(() => window.addEventListener('keydown', onKeydown))
    onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
}

function isTyping(element: Element | null): boolean {
    if (!(element instanceof HTMLElement)) return false
    return EDITABLE_TAGS.has(element.tagName) || element.isContentEditable
}

/** Hidden by CSS (`display: none` up the tree) means no box, hence no offsetParent. */
function isLaidOut(input: HTMLInputElement): boolean {
    return input.offsetParent !== null
}
