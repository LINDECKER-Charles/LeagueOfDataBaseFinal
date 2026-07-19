/**
 * Progressive enhancement for the /profile favorites form: auto-saves on every
 * change (favorite/skin picks dispatch `profile:changed`; the visibility switch
 * fires natively) via a debounced XHR, and live-reflects the visibility state.
 * Without JS the form keeps its manual submit + flash round-trip untouched.
 *
 * Not a Vue island: it wires the plain <form> that HOSTS the islands, so it must
 * survive the islands replacing their own shells on mount.
 */
const DEBOUNCE_MS = 500
const SAVED_LINGER_MS = 2500

interface SaveOutcome {
    ok: boolean
    error?: string
    invalidFavorites?: string[]
    skinInvalid?: boolean
    isPublicProfile?: boolean
}

interface StatusLabels {
    saving: string
    saved: string
    error: string
    dropped: string
}

/* In-memory (not a DOM attribute): a Turbo cache restore brings back a fresh
   element that is correctly absent here, so listeners are re-attached — a cached
   marker attribute would wrongly mark it bound and leave auto-save dead. */
const bound = new WeakSet<HTMLFormElement>()
const versionBound = new WeakSet<HTMLSelectElement>()

export function setupProfileForm(root: ParentNode = document): void {
    root.querySelectorAll<HTMLFormElement>('form[data-autosave]').forEach((form) => {
        if (bound.has(form)) {
            return
        }
        bound.add(form)
        enhance(form)
    })
    wireVersionSelect(root)
}

/* Favorites version picker: auto-submit its standalone form on change (a full
   reload re-resolves favorites at the chosen patch) and drop the no-JS button. */
function wireVersionSelect(root: ParentNode): void {
    root.querySelectorAll<HTMLSelectElement>('[data-version-select]').forEach((select) => {
        if (versionBound.has(select)) {
            return
        }
        versionBound.add(select)
        select
            .closest('.profile-version')
            ?.querySelectorAll<HTMLElement>('[data-version-hide]')
            .forEach((el) => (el.hidden = true))
        select.addEventListener('change', () => select.form?.requestSubmit())
    })
}

function enhance(form: HTMLFormElement): void {
    const status = form.querySelector<HTMLElement>('[data-autosave-status]')
    const labels = readLabels(status)
    form.querySelectorAll<HTMLElement>('[data-autosave-hide]').forEach((el) => (el.hidden = true))
    status?.removeAttribute('hidden')

    let timer = 0
    let clearSaved = 0
    const queueSave = (): void => {
        window.clearTimeout(timer)
        timer = window.setTimeout(() => void save(form, status, labels, (t) => (clearSaved = t)), DEBOUNCE_MS)
    }

    form.addEventListener('profile:changed', queueSave)

    const visibility = form.querySelector<HTMLInputElement>('input[name="isPublicProfile"]')
    visibility?.addEventListener('change', () => {
        syncVisibility(form, visibility.checked)
        window.clearTimeout(clearSaved)
        queueSave()
    })
}

async function save(
    form: HTMLFormElement,
    status: HTMLElement | null,
    labels: StatusLabels,
    onSaved: (timer: number) => void,
): Promise<void> {
    setStatus(status, 'saving', labels.saving)
    try {
        const response = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' },
            credentials: 'same-origin',
        })
        const data = (await response.json().catch(() => ({}))) as SaveOutcome
        if (!response.ok || !data.ok) {
            setStatus(status, 'error', data.error ?? labels.error)
            return
        }
        const dropped = (data.invalidFavorites?.length ?? 0) > 0 || Boolean(data.skinInvalid)
        if (dropped) {
            setStatus(status, 'warned', labels.dropped)
            return
        }
        setStatus(status, 'saved', labels.saved)
        onSaved(window.setTimeout(() => setStatus(status, 'idle', ''), SAVED_LINGER_MS))
    } catch {
        setStatus(status, 'error', labels.error)
    }
}

function syncVisibility(form: HTMLFormElement, isPublic: boolean): void {
    const card = form.querySelector<HTMLElement>('[data-visibility]')
    if (!card) {
        return
    }
    card.classList.toggle('visibility-card--public', isPublic)
    card.classList.toggle('visibility-card--private', !isPublic)

    const state = card.querySelector<HTMLElement>('[data-visibility-state]')
    if (state) {
        state.textContent = isPublic ? (card.dataset.statePublic ?? '') : (card.dataset.statePrivate ?? '')
    }
    card.querySelectorAll<HTMLElement>('[data-visibility-public-only]').forEach((el) => (el.hidden = !isPublic))
}

function setStatus(status: HTMLElement | null, state: 'idle' | 'saving' | 'saved' | 'warned' | 'error', text: string): void {
    if (!status) {
        return
    }
    status.textContent = text
    status.classList.remove('is-saving', 'is-saved', 'is-warned', 'is-error')
    if (state !== 'idle') {
        status.classList.add(`is-${state}`)
    }
}

function readLabels(status: HTMLElement | null): StatusLabels {
    return {
        saving: status?.dataset.labelSaving ?? 'Saving…',
        saved: status?.dataset.labelSaved ?? 'Saved',
        error: status?.dataset.labelError ?? 'Save failed',
        dropped: status?.dataset.labelDropped ?? 'Saved — some favorites were unavailable',
    }
}
