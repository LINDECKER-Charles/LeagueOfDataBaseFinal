/**
 * Turns the server-rendered time-series SVG into an explorable chart: X zoom
 * (wheel / buttons / keyboard), drag panning, and a crosshair whose tooltip
 * names the exact value of every series at the hovered point.
 *
 * Progressive enhancement — the SSR markup is a complete chart on its own, with
 * a `<title>` per point. This module only transforms the `.c-plot` group and
 * drives the untransformed interaction layer built by chart-view.js; it never
 * rewrites the data marks, so a failure here degrades to the static chart.
 */

import { IDENTITY, MIN_SCALE, ZOOM_STEP, indexAt, modelXOf, offsetToReveal, panBy, toModelX, toScreenX, zoomAt } from './chart-scale.js'
import { createLayers, renderAxis, renderHover } from './chart-view.js'

const DRAG_THRESHOLD_PX = 3

export function enhanceCharts(root = document) {
    root.querySelectorAll('.chart-fig[data-chart]:not([data-chart-ready])').forEach(mount)
}

function mount(fig) {
    const model = readModel(fig)
    const svg = fig.querySelector('svg')
    const plot = fig.querySelector('.c-plot')
    const axis = fig.querySelector('.c-xaxis')
    if (!model || !svg || !plot || !axis) {
        return
    }

    fig.dataset.chartReady = 'true'
    fig.classList.add('is-interactive')
    // Focusable so the chart is drivable without a pointer; the SSR role="img"
    // and its label are left alone, and the tooltip is a role="status" region
    // that announces each value as the selection moves.
    svg.setAttribute('tabindex', '0')

    const chart = { fig, model, svg, plot, axis, view: { ...IDENTITY }, hover: null, overlay: null }
    chart.overlay = createLayers(fig, svg, model.box, {
        zoomIn: () => applyZoom(chart, ZOOM_STEP),
        zoomOut: () => applyZoom(chart, 1 / ZOOM_STEP),
        reset: () => reset(chart),
    })
    bindPointer(chart)
    bindKeyboard(chart)
    render(chart)
}

function readModel(fig) {
    try {
        const model = JSON.parse(fig.dataset.chart)

        return model?.dates?.length > 1 ? model : null
    } catch {
        return null
    }
}

// --- Interaction ------------------------------------------------------------

function bindPointer(chart) {
    const { svg, overlay } = chart
    let drag = null

    overlay.hit.addEventListener('wheel', (event) => {
        event.preventDefault()
        applyZoom(chart, event.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP, pointerX(chart, event))
    }, { passive: false })

    overlay.hit.addEventListener('pointerdown', (event) => {
        if (chart.view.scale === MIN_SCALE) {
            return
        }
        drag = { originX: pointerX(chart, event), offset: chart.view.offset }
        overlay.hit.setPointerCapture(event.pointerId)
    })

    svg.addEventListener('pointermove', (event) => {
        const box = chart.model.box
        const x = pointerX(chart, event)
        if (drag && Math.abs(x - drag.originX) > DRAG_THRESHOLD_PX) {
            chart.view = panBy({ scale: chart.view.scale, offset: drag.offset }, box, x - drag.originX)
        }
        const inside = x >= box.padX && x <= box.padX + box.plotW
        chart.hover = inside ? indexAt(toModelX(x, chart.view, box), box, chart.model.dates.length) : null
        render(chart)
    })

    const endDrag = (event) => {
        if (drag && overlay.hit.hasPointerCapture?.(event.pointerId)) {
            overlay.hit.releasePointerCapture(event.pointerId)
        }
        drag = null
    }
    svg.addEventListener('pointerup', endDrag)
    svg.addEventListener('pointercancel', endDrag)
    svg.addEventListener('pointerleave', () => {
        chart.hover = null
        render(chart)
    })
    svg.addEventListener('dblclick', () => reset(chart))
}

const KEY_ACTIONS = {
    ArrowLeft: (chart) => moveHover(chart, -1),
    ArrowRight: (chart) => moveHover(chart, 1),
    '+': (chart) => applyZoom(chart, ZOOM_STEP),
    '=': (chart) => applyZoom(chart, ZOOM_STEP),
    '-': (chart) => applyZoom(chart, 1 / ZOOM_STEP),
    Home: (chart) => reset(chart),
    Escape: (chart) => { chart.hover = null; render(chart) },
}

function bindKeyboard(chart) {
    chart.svg.addEventListener('keydown', (event) => {
        const action = KEY_ACTIONS[event.key]
        if (!action) {
            return
        }
        event.preventDefault()
        action(chart)
    })
}

/** Pointer position in SVG user units (the viewBox maps 1:1 onto the box). */
function pointerX(chart, event) {
    const rect = chart.svg.getBoundingClientRect()

    return ((event.clientX - rect.left) * chart.model.box.w) / (rect.width || 1)
}

/** Zooms around `atX`, falling back to the selected point then the plot centre. */
function applyZoom(chart, factor, atX) {
    const box = chart.model.box
    const anchor = atX ?? (chart.hover !== null
        ? toScreenX(modelXOf(chart.hover, box, chart.model.dates.length), chart.view, box)
        : box.padX + box.plotW / 2)
    chart.view = zoomAt(chart.view, box, anchor, factor)
    render(chart)
}

function reset(chart) {
    chart.view = { ...IDENTITY }
    render(chart)
}

/** Moves the selection and pans just enough to keep it on screen. */
function moveHover(chart, step) {
    const count = chart.model.dates.length
    chart.hover = Math.min(count - 1, Math.max(0, (chart.hover ?? 0) + step))
    const revealed = offsetToReveal(chart.hover, chart.view, chart.model.box, count)
    if (revealed !== null) {
        chart.view = { ...chart.view, offset: revealed }
    }
    render(chart)
}

function render(chart) {
    const { view, model } = chart
    chart.plot.setAttribute(
        'transform',
        `translate(${model.box.padX * (1 - view.scale) + view.offset} 0) scale(${view.scale} 1)`,
    )
    chart.fig.classList.toggle('is-zoomed', view.scale > MIN_SCALE)
    renderAxis(chart)
    renderHover(chart)
}
