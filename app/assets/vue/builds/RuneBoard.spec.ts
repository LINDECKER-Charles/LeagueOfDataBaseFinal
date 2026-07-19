import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import RuneBoard from './RuneBoard.vue'
import type { RuneTree } from './catalogTypes'
import type { RuneDraft } from './runeRules'
import type { RunesLabels, UiLabels } from './editorLabels'

const PRIMARY_ID = 1
const SECONDARY_ID = 2

const runesLabels: RunesLabels = {
    title: 'Runes',
    primary: 'Primaire',
    secondary: 'Secondaire',
    keystone: 'Clé de voûte',
    slot: 'Emplacement %n%',
    secondaryHint: 'Choisis deux runes',
}

const uiLabels: UiLabels = {
    loading: 'Chargement',
    error: 'Erreur',
    retry: 'Réessayer',
    ghost: 'Rune absente du patch',
    ghostMode: 'Exclue du mode',
    counter: '%count% / %max%',
}

function perk(id: number): RuneTree['slots'][number][number] {
    return { id, key: `perk-${id}`, name: `Perk ${id}`, icon: null, shortDesc: '' }
}

/** A minor tree with keystone slot 0 and three selectable minor rows (1..3). */
function tree(id: number, key: string): RuneTree {
    return {
        id,
        key,
        name: key,
        icon: null,
        slots: [
            [perk(id * 1000)],
            [perk(id * 1000 + 11), perk(id * 1000 + 12)],
            [perk(id * 1000 + 21), perk(id * 1000 + 22)],
            [perk(id * 1000 + 31), perk(id * 1000 + 32)],
        ],
    }
}

const trees = [tree(PRIMARY_ID, 'precision'), tree(SECONDARY_ID, 'domination')]

function mountBoard(draft: RuneDraft) {
    return mount(RuneBoard, {
        props: { trees, isLoading: false, hasError: false, draft, labels: runesLabels, ui: uiLabels },
    })
}

function draftWith(secondaryPicks: RuneDraft['secondaryPicks']): RuneDraft {
    return {
        primaryStyleId: PRIMARY_ID,
        primaryPerks: [1000, 1011, 1021, 1031],
        secondaryStyleId: SECONDARY_ID,
        secondaryPicks,
    }
}

/** Second top-level block of the board is the secondary tree section. */
function secondaryBlock(wrapper: ReturnType<typeof mountBoard>): HTMLElement {
    return wrapper.element.querySelectorAll(':scope > div')[1] as HTMLElement
}

describe('RuneBoard — secondary row locking', () => {
    it('greys the single unused secondary row once both picks are taken', () => {
        const wrapper = mountBoard(
            draftWith([
                { slotIndex: 1, perkId: 2011 },
                { slotIndex: 2, perkId: 2021 },
            ]),
        )

        // `forge-slot--locked` is only ever bound on secondary rows, so a global
        // count of 1 already proves exactly one row is dimmed.
        const locked = wrapper.findAll('.forge-slot--locked')
        expect(locked).toHaveLength(1)
        // It is the untouched slot 3, and it is not also marked picked.
        expect(locked[0].find('.forge-slot__name').text()).toBe('Emplacement 3')
        expect(locked[0].classes()).not.toContain('forge-slot--picked')

        // The two used rows stay picked (coloured), not locked.
        const rows = secondaryBlock(wrapper).querySelectorAll('.forge-slot')
        expect(rows).toHaveLength(3)
        expect(rows[0].classList.contains('forge-slot--picked')).toBe(true)
        expect(rows[1].classList.contains('forge-slot--picked')).toBe(true)
        expect(rows[2].classList.contains('forge-slot--locked')).toBe(true)
    })

    it('locks no row while the secondary side is not yet full', () => {
        const wrapper = mountBoard(draftWith([{ slotIndex: 1, perkId: 2011 }]))
        expect(wrapper.findAll('.forge-slot--locked')).toHaveLength(0)
    })
})
