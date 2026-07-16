import { mount } from '@vue/test-utils'
import { describe, it, expect } from 'vitest'
import NumberNav from './NumberNav.vue'

const tpl = '/champions?numpage=__P__&itemperpage=8'

describe('NumberNav', () => {
  it('defaults the href to min when no value is typed', () => {
    const w = mount(NumberNav, { props: { pathTemplate: tpl, min: 1, max: 10 } })
    expect(w.get('a').attributes('href')).toBe('/champions?numpage=1&itemperpage=8')
  })

  it('substitutes the typed value into the placeholder', async () => {
    const w = mount(NumberNav, { props: { pathTemplate: tpl, min: 1, max: 10 } })
    await w.get('input').setValue('5')
    expect(w.get('a').attributes('href')).toBe('/champions?numpage=5&itemperpage=8')
  })

  it('clamps the value to [min, max]', async () => {
    const w = mount(NumberNav, { props: { pathTemplate: tpl, min: 2, max: 6 } })
    const input = w.get('input')
    await input.setValue('99')
    expect(w.get('a').attributes('href')).toBe('/champions?numpage=6&itemperpage=8')
    await input.setValue('0')
    expect(w.get('a').attributes('href')).toBe('/champions?numpage=2&itemperpage=8')
  })

  it('starts from the provided value', () => {
    const w = mount(NumberNav, { props: { pathTemplate: tpl, min: 1, max: 10, value: 4 } })
    expect(w.get('a').attributes('href')).toBe('/champions?numpage=4&itemperpage=8')
  })
})
