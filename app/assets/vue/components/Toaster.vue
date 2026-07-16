<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

/**
 * Flash-message island — replaces the ~140-line inline script in
 * partials/toaster.html.twig. Twig serializes app.flashes into data-props.
 * Messages are app-generated (trusted) and may contain markup, hence v-html.
 */
interface FlashMessage {
    type: 'success' | 'error' | 'warning' | 'info' | string
    text: string
}

const props = defineProps<{ messages?: FlashMessage[] }>()

interface Toast extends FlashMessage {
    id: number
    visible: boolean
}

const toasts = ref<Toast[]>([])
const modal = ref<{ open: boolean; html: string; type: string }>({ open: false, html: '', type: 'info' })
const timers = new Map<number, ReturnType<typeof setTimeout>>()

const ACCENT: Record<string, string> = {
    success: 'border-emerald-400/50 text-emerald-200',
    error: 'border-red-400/50 text-red-200',
    warning: 'border-amber-400/50 text-amber-200',
    info: 'border-hex/50 text-hex-bright',
}

function accent(type: string): string {
    return ACCENT[type] ?? ACCENT.info
}

function dismiss(id: number): void {
    const t = toasts.value.find((x) => x.id === id)
    if (!t) return
    t.visible = false
    const timer = timers.get(id)
    if (timer) clearTimeout(timer)
    timers.delete(id)
    setTimeout(() => {
        toasts.value = toasts.value.filter((x) => x.id !== id)
    }, 220)
}

function openModal(t: Toast): void {
    modal.value = { open: true, html: t.text, type: t.type }
}

function closeModal(): void {
    modal.value.open = false
}

function onKey(e: KeyboardEvent): void {
    if (e.key === 'Escape') closeModal()
}

onMounted(() => {
    document.addEventListener('keydown', onKey)
    ;(props.messages ?? []).forEach((m, i) => {
        const id = i + 1
        toasts.value.push({ ...m, id, visible: false })
        setTimeout(() => {
            const t = toasts.value.find((x) => x.id === id)
            if (t) t.visible = true
        }, 40 + i * 70)
        timers.set(id, setTimeout(() => dismiss(id), 5000))
    })
})

onBeforeUnmount(() => {
    document.removeEventListener('keydown', onKey)
    timers.forEach((t) => clearTimeout(t))
})
</script>

<template>
    <div class="pointer-events-none fixed top-5 right-5 z-50 flex flex-col gap-3">
        <div
            v-for="t in toasts"
            :key="t.id"
            class="hextech-frame pointer-events-auto w-80 cursor-pointer px-4 py-3 transition-all duration-300"
            :class="[accent(t.type), t.visible ? 'translate-x-0 opacity-100' : 'translate-x-6 opacity-0']"
            role="alert"
            @click="openModal(t)"
        >
            <div class="flex items-start gap-3">
                <span class="mt-0.5 h-2 w-2 shrink-0 rounded-full bg-current shadow-[0_0_8px_currentColor]" />
                <div class="flex-1 truncate whitespace-pre-line break-words text-sm leading-5 text-text" v-html="t.text" />
                <button
                    type="button"
                    class="-m-1 ml-1 inline-flex h-6 w-6 items-center justify-center rounded text-text-muted hover:text-gold-bright"
                    :aria-label="'Close'"
                    @click.stop="dismiss(t.id)"
                >
                    <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12" /></svg>
                </button>
            </div>
        </div>
    </div>

    <Teleport to="body">
        <div v-if="modal.open" class="fixed inset-0 z-[60] grid place-items-center">
            <div class="absolute inset-0 bg-hextech-black/70 backdrop-blur-sm" @click="closeModal" />
            <div
                class="hextech-frame relative z-10 max-h-[80vh] w-[min(92vw,44rem)] overflow-hidden"
                :class="accent(modal.type)"
                role="dialog"
                aria-modal="true"
            >
                <div class="flex items-center justify-between gap-4 border-b border-gold-deep/40 px-5 py-4">
                    <h2 class="eyebrow">Message</h2>
                    <button type="button" class="rounded p-1 text-text-muted hover:text-gold-bright" aria-label="Close" @click="closeModal">
                        <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12" /></svg>
                    </button>
                </div>
                <div class="max-h-[68vh] overflow-auto px-5 py-4 text-[15px] leading-6 whitespace-pre-line break-words text-text" v-html="modal.html" />
            </div>
        </div>
    </Teleport>
</template>
