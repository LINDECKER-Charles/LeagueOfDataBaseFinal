import { beforeEach, describe, expect, it } from 'vitest'

import { enhanceCharts } from '../../public/admin/js/chart.js'

// Mirrors what TimeSeriesChart emits (see the PHP side's own test for the
// contract): a clipped `.c-plot` group, an empty `.c-xaxis`, and the raw values
// in `data-chart`.
const MODEL = {
    box: { w: 760, h: 240, padX: 34, padTop: 16, plotW: 692, plotH: 198 },
    max: 9,
    dates: ['2026-07-15', '2026-07-16', '2026-07-17'],
    series: [{ label: 'Vues', color: 'var(--gold)', format: 'int', values: [4, 9, 2] }],
}

function renderServerSide(payload = JSON.stringify(MODEL)) {
    document.body.innerHTML = `
        <figure class="chart-fig" data-chart="${payload.replace(/"/g, '&quot;')}">
            <svg viewBox="0 0 760 240" class="chart" role="img" aria-label="Fréquentation">
                <defs><clipPath id="cp-abc">\
<rect x="34" y="16" width="692" height="198"/></clipPath></defs>
                <g class="c-plot" clip-path="url(#cp-abc)">\
<polyline points="34,100 380,20 726,180"/></g>
                <g class="c-xaxis"></g>
            </svg>
        </figure>`

    return document.querySelector('.chart-fig')
}

function press(key) {
    document.querySelector('svg')
        .dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }))
}

beforeEach(() => {
    document.body.innerHTML = ''
})

describe('enhanceCharts', () => {
    it('adds the interaction layer over the server-rendered chart', () => {
        const fig = renderServerSide()
        enhanceCharts()

        expect(fig.classList.contains('is-interactive')).toBe(true)
        expect(fig.dataset.chartReady).toBe('true')
        expect(fig.querySelectorAll('.chart-tool')).toHaveLength(3)
        expect(fig.querySelector('.c-hit')).not.toBeNull()
        expect(fig.querySelector('svg').getAttribute('tabindex')).toBe('0')
    })

    it('starts at the identity view and labels the full date span', () => {
        const fig = renderServerSide()
        enhanceCharts()

        expect(fig.querySelector('.c-plot').getAttribute('transform'))
            .toBe('translate(0 0) scale(1 1)')
        const ticks = [...fig.querySelectorAll('.c-xaxis text')].map((t) => t.textContent)
        expect(ticks[0]).toBe('07-15')
        expect(ticks.at(-1)).toBe('07-17')
    })

    it('never enhances the same figure twice', () => {
        const fig = renderServerSide()
        enhanceCharts()
        enhanceCharts()

        expect(fig.querySelectorAll('.c-overlay')).toHaveLength(1)
        expect(fig.querySelectorAll('.chart-tools')).toHaveLength(1)
    })

    it('reads the exact value of the selected point from the payload', () => {
        const fig = renderServerSide()
        enhanceCharts()

        press('ArrowRight')

        const tip = fig.querySelector('.chart-tip')
        expect(tip.hidden).toBe(false)
        expect(tip.querySelector('.tip-date').textContent).toBe('2026-07-16')
        expect(tip.querySelector('.tip-row b').textContent).toBe('9')
        expect(fig.querySelectorAll('.c-hover-dot')).toHaveLength(1)
    })

    it('clears the selection on Escape', () => {
        const fig = renderServerSide()
        enhanceCharts()

        press('ArrowRight')
        press('Escape')

        expect(fig.querySelector('.chart-tip').hidden).toBe(true)
        expect(fig.querySelector('.c-overlay').classList.contains('is-active')).toBe(false)
    })

    it('zooms the plot group and comes back on Home', () => {
        const fig = renderServerSide()
        enhanceCharts()
        const plot = fig.querySelector('.c-plot')

        press('+')

        expect(fig.classList.contains('is-zoomed')).toBe(true)
        expect(plot.getAttribute('transform')).not.toBe('translate(0 0) scale(1 1)')

        press('Home')

        expect(fig.classList.contains('is-zoomed')).toBe(false)
        expect(plot.getAttribute('transform')).toBe('translate(0 0) scale(1 1)')
    })

    it('leaves the static chart alone when the payload is unusable', () => {
        const single = renderServerSide(JSON.stringify({ ...MODEL, dates: ['2026-07-15'] }))
        enhanceCharts()
        expect(single.classList.contains('is-interactive')).toBe(false)

        const broken = renderServerSide('not json')
        enhanceCharts()
        expect(broken.classList.contains('is-interactive')).toBe(false)
    })
})
