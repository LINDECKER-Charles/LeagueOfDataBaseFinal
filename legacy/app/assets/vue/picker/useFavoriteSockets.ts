import { computed, ref, type ComputedRef, type Ref } from 'vue'
import type { PickerEntry } from './filterOptions'
import type { SlotType } from './usePickerCatalog'
import { notifyProfileChanged } from '../profile/profileEvents'

/** Entity a socket currently displays (resolved server-side on first render). */
export interface CurrentEntity {
    id: string
    name: string
    image: string | null
}

/** One favorite socket as the Twig form declares it. */
export interface SlotProp {
    type: SlotType
    fieldName: string
    typeLabel: string
    current: CurrentEntity | null
    storedId: string | null
    emptyLabel: string
}

/** A socket ready to render: its slot, what it shows, what it submits. */
export interface FavoriteSocket {
    slot: SlotProp
    index: number
    display: CurrentEntity | null
    value: string
    caption: string
    stateClass: string
}

export interface FavoriteSockets {
    sockets: ComputedRef<FavoriteSocket[]>
    valueAt: (index: number) => string
    assign: (index: number, entry: PickerEntry) => void
    clear: (index: number) => void
}

/**
 * Form-facing state of the four favorite sockets: what each one displays and
 * what its hidden input submits. An unresolvable stored id round-trips
 * untouched so the server warns on save instead of the island silently
 * clearing it — hence the display and the value being tracked apart.
 *
 * `root` is the island element (template ref owned by the SFC); a change is
 * announced to the surrounding profile form from there.
 */
export function useFavoriteSockets(
    slots: SlotProp[],
    unavailable: string,
    root: Readonly<Ref<HTMLElement | null>>,
): FavoriteSockets {
    const values = ref<string[]>(slots.map((slot) => slot.storedId ?? slot.current?.id ?? ''))
    const displays = ref<(CurrentEntity | null)[]>(slots.map((slot) => slot.current))

    const write = (index: number, value: string, display: CurrentEntity | null): void => {
        values.value[index] = value
        displays.value[index] = display
        notifyProfileChanged(root.value)
    }

    return {
        sockets: computed(() => slots.map((slot, index) => describe({
            slot,
            index,
            display: displays.value[index] ?? null,
            value: values.value[index] ?? '',
        }, unavailable))),
        valueAt: (index) => values.value[index] ?? '',
        assign: (index, entry) => write(index, entry.id, {
            id: entry.id,
            name: entry.name,
            image: entry.image,
        }),
        clear: (index) => write(index, '', null),
    }
}

type SocketFacts = Omit<FavoriteSocket, 'caption' | 'stateClass'>

/** Caption and frame of a socket: filled, empty, or holding an unresolvable id. */
function describe(facts: SocketFacts, unavailable: string): FavoriteSocket {
    if (facts.display) {
        return {
            ...facts,
            caption: facts.display.name,
            stateClass: 'hextech-frame hx-corners socket--filled',
        }
    }
    const isUnavailable = facts.value !== ''

    return {
        ...facts,
        caption: isUnavailable ? unavailable : facts.slot.emptyLabel,
        stateClass: isUnavailable ? 'socket--unavailable' : 'socket--empty',
    }
}
