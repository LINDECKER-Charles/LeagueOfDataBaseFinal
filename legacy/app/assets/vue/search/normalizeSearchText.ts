const COMBINING_MARKS = /[\u0300-\u036f]/g

/**
 * The project's single answer to "how do we compare a user's typing to a
 * resource name": lowercased, accent-stripped. Neutral on purpose — every
 * picker (favorites, build editor) must compare alike.
 */
export function normalizeSearchText(value: string): string {
    return value.normalize('NFD').replace(COMBINING_MARKS, '').toLowerCase()
}
