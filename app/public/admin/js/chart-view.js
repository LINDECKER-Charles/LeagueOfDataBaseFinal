/**
 * Presentation layer of the interactive admin chart: builds the pieces the
 * server does not render (interaction overlay, tooltip, zoom toolbar) and
 * redraws them from the current view.
 *
 * Everything here is untransformed — it lives beside the zoomed `.c-plot` group
 * rather than inside it — so dots stay round and labels stay legible whatever
 * the zoom. Interaction state and event wiring live in chart.js.
 */

import { formatValue, axisTicks, modelXOf, toScreenX, visibleRange } from './chart-scale.js'

const SVG_NS = 'http://www.w3.org/2000/svg'
const AXIS_BASELINE_GAP = 16

/**
 * @param {{ zoomIn: () => void, zoomOut: () => void, reset: () => void }} actions
 * @returns {{
 *   group: SVGGElement, crosshair: SVGLineElement, dots: SVGGElement,
 *   hit: SVGRectElement, tip: HTMLElement,
 * }}
 */
export function createLayers(fig, svg, box, actions) {
    const crosshair = svgNode('line', {
        class: 'c-cross', y1: box.padTop, y2: box.padTop + box.plotH, x1: 0, x2: 0,
    })
    const dots = svgNode('g', { class: 'c-hover' })
    // Last child of the overlay: it must sit above the marks to catch pointers.
    const hit = svgNode('rect', {
        class: 'c-hit', x: box.padX, y: box.padTop,
        width: box.plotW, height: box.plotH, fill: 'transparent',
    })
    const group = svgNode('g', { class: 'c-overlay' })
    group.append(crosshair, dots, hit)
    svg.append(group)

    const tip = tooltip()
    fig.append(toolbar(actions), tip)

    return { group, crosshair, dots, hit, tip }
}

export function renderAxis(chart) {
    const { model, view } = chart
    const [from, to] = visibleRange(view, model.box, model.dates.length)
    const baseline = model.box.padTop + model.box.plotH + AXIS_BASELINE_GAP
    const ticks = axisTicks(from, to)
    chart.axis.replaceChildren(...ticks.map((index, position) => {
        const label = svgNode('text', {
            class: 'c-axis',
            x: toScreenX(modelXOf(index, model.box, model.dates.length), view, model.box),
            y: baseline,
            'text-anchor': anchorFor(position, ticks.length),
        })
        label.textContent = (model.dates[index] ?? '').slice(5) // MM-DD

        return label
    }))
}

export function renderHover(chart) {
    const { hover, model, view, overlay } = chart
    if (hover === null) {
        overlay.group.classList.remove('is-active')
        overlay.tip.hidden = true

        return
    }

    const screenX = toScreenX(modelXOf(hover, model.box, model.dates.length), view, model.box)
    overlay.group.classList.add('is-active')
    overlay.crosshair.setAttribute('x1', String(screenX))
    overlay.crosshair.setAttribute('x2', String(screenX))
    overlay.dots.replaceChildren(...model.series.map((serie) => svgNode('circle', {
        class: 'c-hover-dot', cx: screenX,
        cy: valueY(serie.values[hover], model), r: 4, fill: serie.color,
    })))
    renderTip(chart, screenX)
}

function renderTip(chart, screenX) {
    const { model, hover, overlay } = chart
    const date = document.createElement('span')
    date.className = 'tip-date'
    date.textContent = model.dates[hover] ?? ''
    overlay.tip.replaceChildren(date, ...model.series.map((serie) => tipRow(serie, hover)))
    overlay.tip.hidden = false
    placeTip(chart, screenX)
}

function tipRow(serie, index) {
    const row = document.createElement('span')
    row.className = 'tip-row'
    const swatch = document.createElement('span')
    swatch.className = 'sw'
    swatch.style.background = serie.color
    const label = document.createElement('span')
    label.textContent = serie.label
    const value = document.createElement('b')
    value.textContent = formatValue(serie.values[index] ?? 0, serie.format)
    row.append(swatch, label, value)

    return row
}

/** Flips to the left half so a tooltip near the right edge stays on screen. */
function placeTip(chart, screenX) {
    const { overlay, svg, model } = chart
    const rect = svg.getBoundingClientRect()
    const ratio = (rect.width || model.box.w) / model.box.w
    const left = screenX * ratio
    overlay.tip.classList.toggle('is-flipped', left > rect.width / 2)
    overlay.tip.style.left = `${left}px`
    overlay.tip.style.top = `${model.box.padTop * ratio}px`
}

function valueY(value, model) {
    const ratio = model.max > 0 ? value / model.max : 0

    return model.box.padTop + model.box.plotH - ratio * model.box.plotH
}

function anchorFor(position, total) {
    if (position === 0) {
        return 'start'
    }

    return position === total - 1 ? 'end' : 'middle'
}

function tooltip() {
    const tip = document.createElement('div')
    tip.className = 'chart-tip'
    tip.setAttribute('role', 'status')
    tip.hidden = true

    return tip
}

function toolbar(actions) {
    const bar = document.createElement('div')
    bar.className = 'chart-tools'
    bar.append(
        toolButton('−', 'Dézoomer', actions.zoomOut),
        toolButton('+', 'Zoomer', actions.zoomIn),
        toolButton('⟲', 'Réinitialiser le zoom', actions.reset),
    )

    return bar
}

function toolButton(glyph, label, onClick) {
    const button = document.createElement('button')
    button.type = 'button'
    button.className = 'chart-tool'
    button.textContent = glyph
    button.title = label
    button.setAttribute('aria-label', label)
    button.addEventListener('click', onClick)

    return button
}

function svgNode(tag, attrs) {
    const node = document.createElementNS(SVG_NS, tag)
    Object.entries(attrs).forEach(([key, value]) => node.setAttribute(key, String(value)))

    return node
}
