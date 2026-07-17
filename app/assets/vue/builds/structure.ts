/**
 * Build structure contract shared by the editor island and the server:
 * `{championId, runes, steps}` exactly as persisted on the Build entity.
 * Pure state <-> JSON helpers; semantic validation lives server-side
 * (BuildStructureValidator) — the client only guarantees the shape.
 */

export interface BuildRunes {
    primaryStyleId: number
    primarySelections: number[]
    secondaryStyleId: number
    secondarySelections: number[]
}

export interface BuildStep {
    label: string
    note: string | null
    items: string[]
}

export interface BuildStructure {
    championId: string
    runes: BuildRunes
    steps: BuildStep[]
}

function toNumber(value: unknown): number | null {
    if (typeof value === 'number' && Number.isInteger(value)) return value
    if (typeof value === 'string' && value !== '' && String(Number(value)) === value) return Number(value)
    return null
}

function toNumberList(value: unknown): number[] {
    if (!Array.isArray(value)) return []
    return value.map((v) => toNumber(v) ?? 0)
}

function parseRunes(raw: unknown): BuildRunes {
    const r = (raw ?? {}) as Record<string, unknown>
    return {
        primaryStyleId: toNumber(r.primaryStyleId) ?? 0,
        primarySelections: toNumberList(r.primarySelections),
        secondaryStyleId: toNumber(r.secondaryStyleId) ?? 0,
        secondarySelections: toNumberList(r.secondarySelections),
    }
}

function parseStep(raw: unknown): BuildStep {
    const s = (raw ?? {}) as Record<string, unknown>
    const note = typeof s.note === 'string' && s.note.trim() !== '' ? s.note : null
    const items = Array.isArray(s.items) ? s.items.filter((i) => i !== null && i !== undefined).map(String) : []
    return { label: typeof s.label === 'string' ? s.label : '', note, items }
}

/**
 * Lenient parse of a server-provided initial structure (object already decoded
 * by the island props, or a JSON string). Never throws: null means "start blank".
 */
export function parseStructure(raw: unknown): BuildStructure | null {
    let value = raw
    if (typeof value === 'string') {
        try {
            value = JSON.parse(value)
        } catch {
            return null
        }
    }
    if (value === null || typeof value !== 'object' || Array.isArray(value)) return null

    const obj = value as Record<string, unknown>
    return {
        championId: typeof obj.championId === 'string' ? obj.championId : '',
        runes: parseRunes(obj.runes),
        steps: Array.isArray(obj.steps) ? obj.steps.map(parseStep) : [],
    }
}

/** JSON payload of the hidden `structure` input — exactly the persisted shape. */
export function serializeStructure(structure: BuildStructure): string {
    return JSON.stringify({
        championId: structure.championId,
        runes: {
            primaryStyleId: structure.runes.primaryStyleId,
            primarySelections: [...structure.runes.primarySelections],
            secondaryStyleId: structure.runes.secondaryStyleId,
            secondarySelections: [...structure.runes.secondarySelections],
        },
        steps: structure.steps.map((step) => ({
            label: step.label,
            note: step.note,
            items: [...step.items],
        })),
    })
}
