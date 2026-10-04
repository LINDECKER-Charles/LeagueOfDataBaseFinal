import { markImages } from './imageLifecycle'
import { fetchImageStatus, type ImageCandidates } from './imageStatusClient'

/**
 * In-page refresh of the images a cold list render deferred. The server
 * renders each one as a `[data-img-pending]` slot (initials under a sweep)
 * inside a `[data-img-scope]` that names the resource type, the patch, the poll
 * batch size and the `<template data-img-template>` the slot turns into.
 *
 * Viewport-first: a slot is only polled once it approaches the screen, so the
 * hundreds of cards the filter island keeps hidden cost nothing until shown.
 * Each slot is asked about on a backoff schedule; the last attempt is flagged
 * so the server may re-queue the ingestion, and a slot still missing after it
 * settles as plain initials. Everything stops on a Turbo visit.
 */
const SLOT_SELECTOR = '[data-img-pending]'
const SCOPE_SELECTOR = '[data-img-scope]'
const TEMPLATE_SELECTOR = 'template[data-img-template]'
const VIEWPORT_MARGIN = '250px'
/** Delay before each attempt; the length is the attempt budget per slot. */
const ATTEMPT_DELAYS_MS = [1500, 3000, 6000, 12000, 24000] as const
/** Coalesces the slots a scroll reveals at once into a single request. */
const COALESCE_MS = 100
const DEFAULT_BATCH = 48
const STATE_ATTR = 'data-img-state'

interface Scope {
    type: string
    version: string
    batch: number
    template: HTMLTemplateElement | null
}

interface PendingSlot {
    el: HTMLElement
    name: string
    scope: Scope
    attempt: number
    dueAt: number
}

let active: PendingImageRefresher | null = null

export function installPendingImages(): void {
    document.addEventListener('DOMContentLoaded', () => startPendingImages())
    document.addEventListener('turbo:load', () => startPendingImages())
    document.addEventListener('turbo:before-cache', stopPendingImages)
    document.addEventListener('turbo:before-render', stopPendingImages)
}

export function startPendingImages(root: ParentNode = document): void {
    stopPendingImages()
    const slots = collectSlots(root)
    if (slots.length === 0) return
    active = new PendingImageRefresher(slots)
}

export function stopPendingImages(): void {
    active?.stop()
    active = null
}

function collectSlots(root: ParentNode): PendingSlot[] {
    const scopes = new Map<HTMLElement, Scope>()
    const slots: PendingSlot[] = []
    for (const el of root.querySelectorAll<HTMLElement>(SLOT_SELECTOR)) {
        const scopeEl = el.closest<HTMLElement>(SCOPE_SELECTOR)
        const name = el.dataset.imgName
        if (!scopeEl || !name) continue
        let scope = scopes.get(scopeEl)
        if (!scope) {
            scope = readScope(scopeEl)
            scopes.set(scopeEl, scope)
        }
        slots.push({ el, name, scope, attempt: 0, dueAt: 0 })
    }
    return slots
}

function readScope(el: HTMLElement): Scope {
    return {
        type: el.dataset.imgScope ?? '',
        version: el.dataset.imgVersion ?? '',
        batch: Number(el.dataset.imgBatch) || DEFAULT_BATCH,
        template: el.querySelector<HTMLTemplateElement>(TEMPLATE_SELECTOR),
    }
}

class PendingImageRefresher {
    private readonly observer: IntersectionObserver | null
    private readonly queue = new Set<PendingSlot>()
    private readonly abort = new AbortController()
    private timer: ReturnType<typeof setTimeout> | null = null
    private isFlushing = false

    constructor(private readonly slots: PendingSlot[]) {
        this.observer = this.observe()
    }

    stop(): void {
        this.observer?.disconnect()
        if (this.timer !== null) clearTimeout(this.timer)
        this.timer = null
        this.abort.abort()
        this.queue.clear()
    }

    /** Without IntersectionObserver every slot is polled right away. */
    private observe(): IntersectionObserver | null {
        if (!('IntersectionObserver' in window)) {
            this.slots.forEach((slot) => this.enqueue(slot, 0))
            return null
        }
        const byElement = new Map(this.slots.map((slot) => [slot.el, slot]))
        const observer = new IntersectionObserver(
            (entries) => {
                for (const entry of entries) {
                    if (!entry.isIntersecting) continue
                    observer.unobserve(entry.target)
                    const slot = byElement.get(entry.target as HTMLElement)
                    if (slot) this.enqueue(slot, 0)
                }
            },
            { rootMargin: VIEWPORT_MARGIN },
        )
        this.slots.forEach((slot) => observer.observe(slot.el))
        return observer
    }

    private enqueue(slot: PendingSlot, delay: number): void {
        slot.dueAt = Date.now() + delay
        this.queue.add(slot)
        this.schedule(Math.max(delay, COALESCE_MS))
    }

    private schedule(delay: number): void {
        if (this.timer !== null || this.abort.signal.aborted) return
        this.timer = setTimeout(() => {
            this.timer = null
            void this.flush()
        }, delay)
    }

    private async flush(): Promise<void> {
        if (this.isFlushing) return
        this.isFlushing = true
        try {
            const now = Date.now()
            const due = [...this.queue].filter((slot) => slot.dueAt <= now)
            await Promise.all(this.batches(due).map((batch) => this.poll(batch)))
        } finally {
            this.isFlushing = false
        }
        this.scheduleNext()
    }

    /** One request per (scope, attempt), capped at the scope's batch size. */
    private batches(slots: PendingSlot[]): PendingSlot[][] {
        const groups = new Map<string, PendingSlot[]>()
        for (const slot of slots) {
            const key = `${slot.scope.type}|${slot.scope.version}|${slot.attempt}`
            const group = groups.get(key) ?? []
            group.push(slot)
            groups.set(key, group)
        }
        const batches: PendingSlot[][] = []
        for (const group of groups.values()) {
            for (let i = 0; i < group.length; i += group[0].scope.batch) {
                batches.push(group.slice(i, i + group[0].scope.batch))
            }
        }
        return batches
    }

    private async poll(batch: PendingSlot[]): Promise<void> {
        const { scope, attempt } = batch[0]
        const names = [...new Set(batch.map((slot) => slot.name))]
        let status
        try {
            status = await fetchImageStatus(
                { type: scope.type, version: scope.version, names, isLastAttempt: isLast(attempt) },
                this.abort.signal,
            )
        } catch {
            // Transient (network, abort): the schedule below retries or gives up.
            status = { images: {}, pending: names }
        }
        if (this.abort.signal.aborted) return
        for (const slot of batch) {
            if (slot.name in status.images) {
                this.queue.delete(slot)
                settle(slot, status.images[slot.name])
            } else {
                this.retry(slot)
            }
        }
    }

    private retry(slot: PendingSlot): void {
        slot.attempt += 1
        if (slot.attempt >= ATTEMPT_DELAYS_MS.length) {
            this.queue.delete(slot)
            settle(slot, null)
            return
        }
        slot.dueAt = Date.now() + ATTEMPT_DELAYS_MS[slot.attempt]
    }

    private scheduleNext(): void {
        if (this.queue.size === 0) return
        const next = Math.min(...[...this.queue].map((slot) => slot.dueAt))
        this.schedule(Math.max(next - Date.now(), COALESCE_MS))
    }
}

function isLast(attempt: number): boolean {
    return attempt === ATTEMPT_DELAYS_MS.length - 1
}

/** Swap the slot for the rendered picture, or leave it as plain initials. */
function settle(slot: PendingSlot, candidates: ImageCandidates | null): void {
    const picture = candidates ? renderPicture(slot, candidates) : null
    if (!picture) {
        slot.el.removeAttribute('data-img-pending')
        slot.el.setAttribute(STATE_ATTR, 'absent')
        return
    }
    slot.el.replaceWith(picture)
    markImages(picture.parentElement ?? document)
}

function renderPicture(slot: PendingSlot, candidates: ImageCandidates): HTMLElement | null {
    const content = slot.scope.template?.content.firstElementChild
    if (!content) return null
    const node = content.cloneNode(true) as HTMLElement
    const img = node.querySelector('img')
    if (!img) return null
    img.setAttribute('src', candidates.src)
    img.setAttribute('alt', slot.el.dataset.imgAlt ?? '')
    const source = node.querySelector('source')
    if (source) {
        candidates.webp ? source.setAttribute('srcset', candidates.webp) : source.remove()
    }
    return node
}
