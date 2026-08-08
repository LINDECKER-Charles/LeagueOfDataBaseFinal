/**
 * Pure viewport math for the admin time-series charts — no DOM, no state.
 *
 * The server renders the marks in a fixed SVG user space (see TimeSeriesChart):
 * point `i` sits at `padX + i * plotW / (n - 1)`. Zooming keeps that geometry
 * untouched and instead applies `translate(padX * (1 - scale) + offset) scale(scale, 1)`
 * to the plot group, so every conversion below is the algebra of that one
 * transform. Only the X axis zooms: the Y grid labels stay valid as rendered.
 */

export const MIN_SCALE = 1
export const MAX_SCALE = 24
export const ZOOM_STEP = 1.25

/**
 * @typedef {{
 *   padX: number, padTop: number, plotW: number, plotH: number, w: number, h: number
 * }} Box
 */
/** @typedef {{ scale: number, offset: number }} View */

export const IDENTITY = { scale: MIN_SCALE, offset: 0 }

export function clampScale(scale) {
    return Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale))
}

/**
 * Keep both plot edges pinned outside the viewport: the left edge may never
 * drift right of the axis, nor the right edge left of it — panning can only ever
 * reveal data, never blank margin.
 */
export function clampOffset(offset, scale, plotW) {
    return Math.min(0, Math.max(plotW * (1 - scale), offset))
}

export function toScreenX(modelX, view, box) {
    return view.scale * modelX + box.padX * (1 - view.scale) + view.offset
}

export function toModelX(screenX, view, box) {
    return (screenX - box.padX * (1 - view.scale) - view.offset) / view.scale
}

/** Zoom by `factor` while keeping the point under `screenX` visually fixed. */
export function zoomAt(view, box, screenX, factor) {
    const scale = clampScale(view.scale * factor)
    const anchor = toModelX(screenX, view, box)
    const offset = clampOffset(screenX - box.padX * (1 - scale) - scale * anchor, scale, box.plotW)

    return { scale, offset }
}

export function panBy(view, box, deltaScreenX) {
    return {
        scale: view.scale,
        offset: clampOffset(view.offset + deltaScreenX, view.scale, box.plotW)
    }
}

export function modelXOf(index, box, count) {
    return box.padX + (count === 1 ? box.plotW / 2 : (index * box.plotW) / (count - 1))
}

export function indexAt(modelX, box, count) {
    if (count < 2) {
        return 0
    }
    const raw = Math.round(((modelX - box.padX) * (count - 1)) / box.plotW)

    return Math.min(count - 1, Math.max(0, raw))
}

/** First and last data index currently inside the plot window (inclusive). */
export function visibleRange(view, box, count) {
    const from = indexAt(toModelX(box.padX, view, box), box, count)
    const to = indexAt(toModelX(box.padX + box.plotW, view, box), box, count)

    return [from, Math.max(from, to)]
}

/** Offset that brings `index` back inside the window, or null when already visible. */
export function offsetToReveal(index, view, box, count) {
    const screenX = toScreenX(modelXOf(index, box, count), view, box)
    if (screenX >= box.padX && screenX <= box.padX + box.plotW) {
        return null
    }
    const target = screenX < box.padX ? box.padX : box.padX + box.plotW

    return clampOffset(view.offset + (target - screenX), view.scale, box.plotW)
}

/** Evenly spaced tick indices inside [from, to], bounds always included. */
export function axisTicks(from, to, wanted = 4) {
    const span = to - from
    if (span <= 0) {
        return [from]
    }
    const steps = Math.min(wanted - 1, span)
    const ticks = []
    for (let s = 0; s <= steps; s++) {
        ticks.push(from + Math.round((s * span) / steps))
    }

    return [...new Set(ticks)]
}

const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']

export function formatValue(value, format) {
    if (format !== 'bytes') {
        return Math.round(value).toLocaleString('fr-FR')
    }
    let n = value
    let unit = 0
    while (n >= 1024 && unit < BYTE_UNITS.length - 1) {
        n /= 1024
        unit++
    }

    return `${unit === 0 ? Math.round(n) : n.toFixed(2)} ${BYTE_UNITS[unit]}`
}
