<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { evaluatePasswordRules, passwordsMatch, type PasswordRuleId } from '../security/passwordRules'

/**
 * Reactive CNIL checklist. The two password inputs belong to the surrounding
 * Twig form — the island only observes them by selector, so the no-JS form
 * keeps working untouched. Before the first blur unmet criteria stay muted
 * (never an aggressive red while the user is still typing).
 */
const props = defineProps<{
    passwordSelector: string
    confirmSelector: string
    labels: Record<PasswordRuleId | 'match', string>
}>()

const password = ref('')
const confirmation = ref('')
const touched = ref(false)

let passwordInput: HTMLInputElement | null = null
let confirmInput: HTMLInputElement | null = null

function sync(): void {
    password.value = passwordInput?.value ?? ''
    confirmation.value = confirmInput?.value ?? ''
}

function markTouched(): void {
    touched.value = true
}

onMounted(() => {
    passwordInput = document.querySelector<HTMLInputElement>(props.passwordSelector)
    confirmInput = document.querySelector<HTMLInputElement>(props.confirmSelector)
    sync()
    passwordInput?.addEventListener('input', sync)
    confirmInput?.addEventListener('input', sync)
    passwordInput?.addEventListener('blur', markTouched)
})

onBeforeUnmount(() => {
    passwordInput?.removeEventListener('input', sync)
    confirmInput?.removeEventListener('input', sync)
    passwordInput?.removeEventListener('blur', markTouched)
})

const items = computed(() => [
    ...evaluatePasswordRules(password.value).map((rule) => ({
        id: rule.id as string,
        label: props.labels[rule.id],
        ok: rule.satisfied,
    })),
    { id: 'match', label: props.labels.match, ok: passwordsMatch(password.value, confirmation.value) },
])
</script>

<template>
    <ul class="pwd-checklist" :class="{ 'pwd-checklist--touched': touched }" aria-live="polite">
        <li
            v-for="item in items"
            :key="item.id"
            class="pwd-checklist__item"
            :class="{ 'pwd-checklist__item--ok': item.ok }"
        >
            <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
                <path
                    v-if="item.ok"
                    d="M2 6.2 5 9l5-6"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="1.6"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                />
                <path v-else d="M6 1.5 10.5 6 6 10.5 1.5 6Z" fill="none" stroke="currentColor" stroke-width="1.2" />
            </svg>
            {{ item.label }}
        </li>
    </ul>
</template>
