import {
    isChoiceSelection,
    isRangeSelection,
    type FacetDefinition,
    type FacetSelection,
    type FacetState,
} from '../facets'
import { PAGE_SIZE_ALL } from '../visibleCards'

/**
 * The filter state ⇄ the page URL, so a filtered list is a shareable link.
 * Canonical on the way out (schema order, sorted values, defaults omitted,
 * both range bounds explicit) so one state has exactly one URL; lenient on
 * the way in. Parameters the filter does not own (`version`, `lang`…) are
 * carried through untouched. Pure: no history, no DOM.
 */
export const QUERY_PARAM = 'q'
export const PAGE_PARAM = 'page'
export const SIZE_PARAM = 'size'
/** `<key>_all=1` — the choice facet must match every selected value. */
const MATCH_ALL_SUFFIX = '_all'
const LIST_SEPARATOR = ','
const RANGE_SEPARATOR = '-'
const FLAG = '1'
const FIRST_PAGE = 1
/** Largest meaningful precision of a facet value (attack speed: 0.625). */
const RANGE_DECIMALS = 3

export interface FilterUrlState {
    query: string
    facets: FacetState
    page: number
    size: number
}

export function parseFilterUrl(
    search: string,
    schema: readonly FacetDefinition[],
    defaultSize: number,
): FilterUrlState {
    const params = new URLSearchParams(search)
    const facets: FacetState = {}
    for (const facet of schema) {
        const selection = parseSelection(params, facet)
        if (selection !== undefined) facets[facet.key] = selection
    }
    return {
        query: params.get(QUERY_PARAM) ?? '',
        facets,
        page: parsePositiveInt(params.get(PAGE_PARAM)) ?? FIRST_PAGE,
        size: parseSize(params.get(SIZE_PARAM)) ?? defaultSize,
    }
}

/** The search string to write back: foreign params first, then the filter's, canonically. */
export function writeFilterUrl(
    search: string,
    state: FilterUrlState,
    schema: readonly FacetDefinition[],
    defaultSize: number,
): string {
    const owned = new Set([QUERY_PARAM, PAGE_PARAM, SIZE_PARAM])
    schema.forEach((facet) => owned.add(facet.key).add(facet.key + MATCH_ALL_SUFFIX))

    const params = new URLSearchParams()
    for (const [name, value] of new URLSearchParams(search)) {
        if (!owned.has(name)) params.append(name, value)
    }
    if (state.query.trim() !== '') params.set(QUERY_PARAM, state.query.trim())
    for (const facet of schema) {
        writeSelection(params, facet, state.facets[facet.key])
    }
    if (state.page > FIRST_PAGE) params.set(PAGE_PARAM, String(state.page))
    if (state.size !== defaultSize) params.set(SIZE_PARAM, serializeSize(state.size))
    const out = params.toString()
    return out ? `?${out}` : ''
}

function parseSelection(params: URLSearchParams, facet: FacetDefinition): FacetSelection | undefined {
    const raw = params.get(facet.key)
    if (raw === null || raw === '') return undefined
    switch (facet.kind) {
        case 'choice': {
            const values = [...new Set(raw.split(LIST_SEPARATOR).filter(Boolean))]
            if (values.length === 0) return undefined
            return {
                values: facet.multiple ? values : values.slice(0, 1),
                all: facet.matchAll && params.get(facet.key + MATCH_ALL_SUFFIX) === FLAG,
            }
        }
        case 'range': {
            const [min, max] = raw.split(RANGE_SEPARATOR, 2).map(Number)
            if (!Number.isFinite(min) || !Number.isFinite(max) || min > max) return undefined
            return { min, max }
        }
        case 'toggle':
            return raw === FLAG ? true : undefined
    }
}

function writeSelection(
    params: URLSearchParams,
    facet: FacetDefinition,
    selection: FacetSelection | undefined,
): void {
    if (selection === undefined) return
    if (selection === true) {
        params.set(facet.key, FLAG)
    } else if (isRangeSelection(selection)) {
        params.set(facet.key, [selection.min, selection.max].map(formatNumber).join(RANGE_SEPARATOR))
    } else if (isChoiceSelection(selection) && selection.values.length > 0) {
        params.set(facet.key, [...selection.values].sort().join(LIST_SEPARATOR))
        if (selection.all && facet.matchAll) params.set(facet.key + MATCH_ALL_SUFFIX, FLAG)
    }
}

function formatNumber(value: number): string {
    return String(Number(value.toFixed(RANGE_DECIMALS)))
}

function parsePositiveInt(raw: string | null): number | undefined {
    const value = Number(raw)
    return Number.isInteger(value) && value > 0 ? value : undefined
}

function parseSize(raw: string | null): number | undefined {
    if (raw === 'all') return PAGE_SIZE_ALL
    return parsePositiveInt(raw)
}

function serializeSize(size: number): string {
    return size === PAGE_SIZE_ALL ? 'all' : String(size)
}
