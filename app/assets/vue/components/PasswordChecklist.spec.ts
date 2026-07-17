import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import PasswordChecklist from './PasswordChecklist.vue'

const LABELS = {
    length: '12+ chars',
    lowercase: 'lower',
    uppercase: 'upper',
    digit: 'digit',
    special: 'special',
    match: 'match',
}

let wrapper: VueWrapper | null = null

function mountWithInputs(): { pwd: HTMLInputElement; confirm: HTMLInputElement; w: VueWrapper } {
    document.body.innerHTML = '<input id="pwd" type="password"><input id="confirm" type="password">'
    const host = document.createElement('div')
    document.body.appendChild(host)
    wrapper = mount(PasswordChecklist, {
        props: { passwordSelector: '#pwd', confirmSelector: '#confirm', labels: LABELS },
        attachTo: host,
    })
    return {
        pwd: document.querySelector<HTMLInputElement>('#pwd')!,
        confirm: document.querySelector<HTMLInputElement>('#confirm')!,
        w: wrapper,
    }
}

function type(input: HTMLInputElement, value: string): void {
    input.value = value
    input.dispatchEvent(new Event('input', { bubbles: true }))
}

afterEach(() => {
    wrapper?.unmount()
    wrapper = null
    document.body.innerHTML = ''
})

describe('PasswordChecklist', () => {
    it('renders the five CNIL criteria plus the match line, all unmet initially', () => {
        const { w } = mountWithInputs()
        const items = w.findAll('.pwd-checklist__item')
        expect(items).toHaveLength(6)
        expect(w.findAll('.pwd-checklist__item--ok')).toHaveLength(0)
    })

    it('reacts to typing in the observed inputs', async () => {
        const { pwd, confirm, w } = mountWithInputs()
        type(pwd, 'Str0ng-passphrase!')
        type(confirm, 'Str0ng-passphrase!')
        await flushPromises()
        expect(w.findAll('.pwd-checklist__item--ok')).toHaveLength(6)
    })

    it('keeps the match line unmet while the fields differ', async () => {
        const { pwd, confirm, w } = mountWithInputs()
        type(pwd, 'Str0ng-passphrase!')
        type(confirm, 'other')
        await flushPromises()
        const okLabels = w.findAll('.pwd-checklist__item--ok').map((item) => item.text())
        expect(okLabels).not.toContain(LABELS.match)
        expect(okLabels).toContain(LABELS.length)
    })

    it('switches to the touched styling only after the password field blurs', async () => {
        const { pwd, w } = mountWithInputs()
        expect(w.find('.pwd-checklist').classes()).not.toContain('pwd-checklist--touched')
        pwd.dispatchEvent(new Event('blur'))
        await flushPromises()
        expect(w.find('.pwd-checklist').classes()).toContain('pwd-checklist--touched')
    })
})
