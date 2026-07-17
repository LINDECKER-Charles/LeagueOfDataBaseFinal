/**
 * Labels contract of the build-editor island (translated server-side, passed as
 * island props) plus the tiny %placeholder% interpolation the UI strings use.
 * Types only describe the wire shape — the source of truth is editor.html.twig.
 */

/** Shared catalog-state wording (loading / error / retry) + ghosts + counters. */
export interface UiLabels {
    loading: string
    error: string
    retry: string
    /** Item id unknown to the selected patch. */
    ghost: string
    /** Item known, but excluded by the selected game mode. */
    ghostMode: string
    counter: string
}

export interface ContextLabels {
    title: string
    version: string
    mode: string
    modeHint: string
}

export interface ChampionLabels {
    title: string
    search: string
    empty: string
    selected: string
}

export interface RunesLabels {
    title: string
    primary: string
    secondary: string
    keystone: string
    slot: string
    secondaryHint: string
}

export interface StepsLabels {
    title: string
    add: string
    remove: string
    moveUp: string
    moveDown: string
    label: string
    note: string
    searchItem: string
    itemEmpty: string
    removeItem: string
    gold: string
    presets: string[]
}

/** Drag-and-drop affordance + polite aria-live announcements. */
export interface DndLabels {
    handle: string
    movedStep: string
    movedItem: string
    transferred: string
    added: string
    cancelled: string
}

/** Labels contract of the build-editor island (translated server-side). */
export interface BuildEditorLabels extends UiLabels {
    context: ContextLabels
    champion: ChampionLabels
    runes: RunesLabels
    steps: StepsLabels
    dnd: DndLabels
}

/** "%key%" template substitution shared by counters and announcements. */
export function formatTemplate(template: string, params: Record<string, string | number>): string {
    return Object.entries(params).reduce(
        (out, [key, value]) => out.split(`%${key}%`).join(String(value)),
        template,
    )
}

/** "%count% / %max%" template substitution for the limit counters. */
export function formatCounter(template: string, count: number, max: number): string {
    return formatTemplate(template, { count, max })
}
