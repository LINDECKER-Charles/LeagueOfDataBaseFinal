import { describe, expect, it } from 'vitest'

import {
    IDENTITY, MAX_SCALE, MIN_SCALE,
    axisTicks, clampOffset, clampScale, formatValue, indexAt, modelXOf, offsetToReveal, panBy,
    toModelX, toScreenX, visibleRange, zoomAt,
} from '../../public/admin/js/chart-scale.js'

// Mirrors TimeSeriesChart's server-side geometry (SvgPrimitives constants).
const BOX = { w: 760, h: 240, padX: 34, padTop: 16, plotW: 692, plotH: 198 }
const COUNT = 30
const LEFT = BOX.padX
const RIGHT = BOX.padX + BOX.plotW

describe('clampScale', () => {
    it('never zooms out past the natural width nor past the hard ceiling', () => {
        expect(clampScale(0.2)).toBe(MIN_SCALE)
        expect(clampScale(1000)).toBe(MAX_SCALE)
        expect(clampScale(3)).toBe(3)
    })
})

describe('clampOffset', () => {
    it('pins both plot edges outside the viewport', () => {
        expect(clampOffset(50, 4, BOX.plotW)).toBe(0)
        expect(clampOffset(-99999, 4, BOX.plotW)).toBe(BOX.plotW * -3)
    })

    it('collapses to zero at natural scale — there is nothing to pan', () => {
        expect(clampOffset(-120, MIN_SCALE, BOX.plotW)).toBe(0)
    })
})

describe('coordinate conversion', () => {
    it('is the identity transform before any zoom', () => {
        expect(toScreenX(200, IDENTITY, BOX)).toBe(200)
        expect(toModelX(200, IDENTITY, BOX)).toBe(200)
    })

    it('round-trips through an arbitrary view', () => {
        const view = { scale: 3.5, offset: -420 }

        expect(toModelX(toScreenX(317, view, BOX), view, BOX)).toBeCloseTo(317, 6)
    })
})

describe('zoomAt', () => {
    it('keeps the point under the cursor visually fixed', () => {
        const cursor = 400
        const anchor = toModelX(cursor, IDENTITY, BOX)
        const zoomed = zoomAt(IDENTITY, BOX, cursor, 2)

        expect(toScreenX(anchor, zoomed, BOX)).toBeCloseTo(cursor, 6)
    })

    it('re-clamps when the anchor would drag an edge inside the plot', () => {
        const zoomed = zoomAt(IDENTITY, BOX, LEFT, 4)

        expect(zoomed.offset).toBe(0)
        expect(toScreenX(BOX.padX, zoomed, BOX)).toBeCloseTo(LEFT, 6)
    })

    it('returns to the identity view when zooming all the way out', () => {
        const zoomed = zoomAt(IDENTITY, BOX, 500, 6)

        expect(zoomAt(zoomed, BOX, 500, 1 / 100)).toEqual({ scale: MIN_SCALE, offset: 0 })
    })
})

describe('panBy', () => {
    it('cannot slide the data away from the axis', () => {
        const zoomed = { scale: 2, offset: -100 }

        expect(panBy(zoomed, BOX, 5000).offset).toBe(0)
        expect(panBy(zoomed, BOX, -5000).offset).toBe(-BOX.plotW)
    })
})

describe('index mapping', () => {
    it('maps the first and last point onto the plot edges', () => {
        expect(modelXOf(0, BOX, COUNT)).toBe(LEFT)
        expect(modelXOf(COUNT - 1, BOX, COUNT)).toBe(RIGHT)
    })

    it('centres a single point', () => {
        expect(modelXOf(0, BOX, 1)).toBe(LEFT + BOX.plotW / 2)
    })

    it('clamps out-of-plot positions to a real index', () => {
        expect(indexAt(-500, BOX, COUNT)).toBe(0)
        expect(indexAt(5000, BOX, COUNT)).toBe(COUNT - 1)
        expect(indexAt(modelXOf(7, BOX, COUNT), BOX, COUNT)).toBe(7)
    })
})

describe('visibleRange', () => {
    it('spans the whole series at natural scale', () => {
        expect(visibleRange(IDENTITY, BOX, COUNT)).toEqual([0, COUNT - 1])
    })

    it('narrows to the zoomed window', () => {
        const [from, to] = visibleRange({ scale: 4, offset: -BOX.plotW * 1.5 }, BOX, COUNT)

        expect(from).toBeGreaterThan(0)
        expect(to).toBeLessThan(COUNT - 1)
        expect(to).toBeGreaterThan(from)
    })
})

describe('offsetToReveal', () => {
    it('reports nothing to do while the point is on screen', () => {
        expect(offsetToReveal(10, IDENTITY, BOX, COUNT)).toBeNull()
    })

    it('slides the window just enough to expose an off-screen point', () => {
        const view = { scale: 4, offset: 0 }
        const revealed = offsetToReveal(COUNT - 1, view, BOX, COUNT)

        expect(revealed).not.toBeNull()
        const moved = { scale: view.scale, offset: revealed }
        expect(toScreenX(modelXOf(COUNT - 1, BOX, COUNT), moved, BOX)).toBeCloseTo(RIGHT, 6)
    })
})

describe('axisTicks', () => {
    it('always includes both bounds', () => {
        const ticks = axisTicks(4, 22)

        expect(ticks[0]).toBe(4)
        expect(ticks.at(-1)).toBe(22)
    })

    it('never repeats a tick on a narrow window', () => {
        expect(axisTicks(6, 7)).toEqual([6, 7])
        expect(axisTicks(6, 6)).toEqual([6])
    })
})

describe('formatValue', () => {
    it('formats counts with a thousands separator', () => {
        expect(formatValue(1234, 'int').replace(/ | /g, ' ')).toBe('1 234')
    })

    it('formats byte series with a binary unit', () => {
        expect(formatValue(2048, 'bytes')).toBe('2.00 KB')
        expect(formatValue(512, 'bytes')).toBe('512 B')
    })
})
