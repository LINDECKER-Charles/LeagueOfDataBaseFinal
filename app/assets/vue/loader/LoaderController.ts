import {
    parseStreamPayload,
    prepareUrl,
    resolveVersionAndLang,
    resourcesFor,
    TYPE_TO_KEY,
    turbo,
    warmKey,
    type ResourceKey,
} from './urls'
import { CategoryProgress } from './CategoryProgress'
import {
    appendEntry,
    beginRun,
    completeRun,
    hideOverlay,
    markReady,
    type LoaderState,
} from './loaderState'
import type { WarmRequestDetail } from './warmBridge'

const READY_HOLD = 450 // once shown, hold 100%/"ready" long enough to read
const WATCHDOG_IDLE = 15000 // no stream activity for this long → assume it's dead and visit anyway

export interface Selection {
    version: string
    lang: string
}

/** What the current run is warming — the identity of the held-back visit. */
interface RunTarget extends Selection {
    url: string
}

interface RunOptions {
    /** Raise the overlay up-front instead of waiting for real image work. */
    eager?: boolean
    /** Resources of a warm token, which no navigable route would resolve. */
    activeKeys?: ResourceKey[]
    /** Set for a warm-in-place: the run hands back instead of visiting. */
    onComplete?: () => void
}

/**
 * SSE "gate-then-visit" state machine of the navigation loader: EventSource
 * lifecycle, inactivity watchdog and held-back visit, driving the reactive
 * {@link LoaderState} the island renders. This much per-run mutable state reads
 * better as one object with named transitions than as a pile of closures;
 * {@link bindLoaderHooks} feeds it the Turbo events.
 */
export class LoaderController {
    private stream: EventSource | null = null
    private generation = 0
    private holdTimer: ReturnType<typeof setTimeout> | undefined
    private watchdog: ReturnType<typeof setTimeout> | undefined
    private bypassUrl: string | null = null
    // Set for an in-place warm (build editor): run to completion, then hand back
    // to the caller instead of performing a Turbo visit.
    private onComplete: (() => void) | null = null
    private finished = false
    private target: RunTarget = { url: '', version: '', lang: '' }
    private readonly warmed = new Set<string>()
    private readonly categories = new CategoryProgress()

    constructor(private readonly state: LoaderState) {}

    /**
     * Claim a cold resource visit — true when this loader holds the navigation
     * back (the caller then prevents the default one). A warm destination, an
     * unknown selection or our own gated visit are all let through untouched.
     */
    gateVisit(url: string): boolean {
        if (url === this.bypassUrl) { this.bypassUrl = null; return false } // our gated visit
        const path = new URL(url, window.location.origin).pathname
        if (!resourcesFor(path).length) return false
        const { version, lang } = resolveVersionAndLang(url)
        if (!version || !lang) return false // no known selection (deferral covers it)
        if (this.warmed.has(warmKey(url, version, lang))) return false // warmed this session
        this.startRun(url, { version, lang })

        return true
    }

    /**
     * A deliberate patch/language switch ALWAYS lands on a cold render, so the
     * overlay goes up at once instead of waiting for the stream to prove work.
     */
    gateSwitch(destUrl: string, selection: Selection): void {
        this.startRun(destUrl, selection, { eager: true })
    }

    /** Overlay up before the prefs round-trip, so the feedback is instant. */
    prepareSwitch(destUrl: string): void {
        this.closeStream()
        this.clearTimers()
        this.resetRun(new URL(destUrl, window.location.origin).pathname)
        this.state.visible.value = true
    }

    /** Warm a (version, lang) for another island and hand back to it — no visit. */
    warmInPlace(request: WarmRequestDetail): void {
        this.startRun(
            request.path,
            { version: request.version, lang: request.lang },
            { activeKeys: resourcesFor(request.path), onComplete: request.resolve },
        )
    }

    /** The (warm) destination has rendered: the overlay leaves when it is ready. */
    dismiss(): void {
        if (!this.state.visible.value && this.state.phase.value === 'idle') return
        this.stop()
        hideOverlay(this.state)
    }

    /** Drop every pending timer and the open stream (island teardown). */
    stop(): void {
        this.clearTimers()
        this.closeStream()
    }

    private startRun(destUrl: string, override?: Selection, opts?: RunOptions): void {
        const { version, lang } = resolveVersionAndLang(destUrl, override)
        this.adoptCompletion(opts?.onComplete ?? null)
        if (!version || !lang) { this.finishWithoutWork(destUrl); return }

        const runId = ++this.generation
        this.stop()
        this.target = { url: destUrl, version, lang }
        this.resetRun(new URL(destUrl, window.location.origin).pathname, opts?.activeKeys)

        // A batch-less destination (detail, profile…) has nothing to stream, so skip
        // the SSE and let the eager overlay cover the cold server render until the
        // destination's turbo:load hides it. Plain navigations keep the honest
        // "surface only on real image work (total > 0)" rule of handleStart().
        if (opts?.eager) {
            this.state.visible.value = true
            if (this.state.active.value.length === 0) {
                this.finishWithoutWork(destUrl)
                return
            }
        }

        this.rearmWatchdog()
        this.openStream(runId)
    }

    private openStream(runId: number): void {
        const { url, version, lang } = this.target
        try {
            this.stream = new EventSource(prepareUrl(url, version, lang))
        } catch {
            this.finish(runId)
            return
        }
        this.subscribe(this.stream, runId)
    }

    private subscribe(stream: EventSource, runId: number): void {
        stream.addEventListener('start', (ev) => {
            if (this.accepts(runId)) this.handleStart(parseStreamPayload(ev))
        })
        // The dataset phase (cold JSON fetch) emits `phase` before `start`; keep the
        // watchdog alive across it so a slow-but-progressing warm isn't cut.
        stream.addEventListener('phase', () => {
            if (this.accepts(runId)) this.rearmWatchdog()
        })
        stream.addEventListener('item', (ev) => {
            if (this.accepts(runId)) this.handleItem(parseStreamPayload(ev))
        })
        stream.addEventListener('done', () => this.finish(runId))
        // Opaque + auto-reconnecting: on a real failure (not our own close after
        // done) bail out to a normal visit so navigation never gets stuck.
        stream.onerror = () => {
            if (!this.finished) this.finish(runId)
        }
    }

    /** Events of a superseded run are ignored: a newer one owns the overlay. */
    private accepts(runId: number): boolean {
        return runId === this.generation
    }

    private handleStart(payload: Record<string, unknown>): void {
        this.rearmWatchdog()
        const totals = (payload.categories ?? {}) as Record<string, number>
        this.categories.seed(totals, (key) => markReady(this.state, key))
        this.state.phase.value = 'loading'
        // Surface the overlay only when there is genuine Riot image warming to
        // show; a warm destination (total 0) resolves straight to the visit.
        if (payload.total) this.state.visible.value = true
        else this.state.progress.value = 1
    }

    private handleItem(payload: Record<string, unknown>): void {
        this.rearmWatchdog()
        const key = TYPE_TO_KEY[String(payload.category)] ?? this.state.active.value[0]
        appendEntry(this.state, String(payload.name ?? ''), key)
        if (payload.total) {
            this.state.progress.value = Math.min(1, Number(payload.index) / Number(payload.total))
        }
        if (key && this.categories.record(key)) markReady(this.state, key)
    }

    /**
     * Inactivity watchdog: a cold page legitimately outlasts any fixed budget, so
     * we don't cap the total — we only bail if the stream falls silent. Every
     * stream event re-arms it, so a long warm that keeps progressing is never cut.
     */
    private rearmWatchdog(): void {
        if (this.watchdog) clearTimeout(this.watchdog)
        const runId = this.generation
        this.watchdog = setTimeout(() => this.finish(runId), WATCHDOG_IDLE)
    }

    private finish(runId: number): void {
        if (this.finished || !this.accepts(runId)) return
        this.finished = true
        this.closeStream()
        if (this.watchdog) { clearTimeout(this.watchdog); this.watchdog = undefined }

        const { url, version, lang } = this.target
        completeRun(this.state)
        this.warmed.add(warmKey(url, version, lang))

        const resume = this.takeCompletion()
        if (resume) { this.handBack(resume); return }
        this.holdReady(() => this.navigateWarm(url))
    }

    /**
     * In-place warm (build editor): resume the caller instead of visiting. The
     * reload is kicked BEFORE hiding so the pickers swap to their loading state
     * under the overlay, never flashing stale rows.
     */
    private handBack(resume: () => void): void {
        if (!this.state.visible.value) {
            hideOverlay(this.state)
            resume()
            return
        }
        this.holdReady(() => { resume(); hideOverlay(this.state) })
    }

    /** Hold the "ready" beat long enough to read — only if the overlay surfaced. */
    private holdReady(action: () => void): void {
        if (!this.state.visible.value) { action(); return }
        this.holdTimer = setTimeout(() => { this.holdTimer = undefined; action() }, READY_HOLD)
    }

    /** Perform the gated visit; the re-fired before-visit is recognised by URL. */
    private navigateWarm(url: string): void {
        this.bypassUrl = url
        const turboApi = turbo()
        if (turboApi?.visit) turboApi.visit(url)
        else window.location.assign(url)
        this.bypassUrl = null
    }

    /** End a run with no image work: an in-place warm hands back, a nav visits. */
    private finishWithoutWork(destUrl: string): void {
        const resume = this.takeCompletion()
        if (resume) { hideOverlay(this.state); resume(); return }
        this.navigateWarm(destUrl)
    }

    /**
     * A new run supersedes any still-pending in-place warm; resolve its callback
     * so the awaiting caller (build editor) is never left hanging.
     */
    private adoptCompletion(next: (() => void) | null): void {
        const superseded = this.onComplete
        this.onComplete = next
        if (superseded && superseded !== next) superseded()
    }

    private takeCompletion(): (() => void) | null {
        const resume = this.onComplete
        this.onComplete = null
        return resume
    }

    private resetRun(path: string, activeKeys?: ResourceKey[]): void {
        this.finished = false
        // A warm-in-place run names its own resources (the nav routes never render
        // this token); everything else derives them from the destination path.
        beginRun(this.state, activeKeys ?? resourcesFor(path))
        this.categories.reset()
    }

    private clearTimers(): void {
        if (this.holdTimer) { clearTimeout(this.holdTimer); this.holdTimer = undefined }
        if (this.watchdog) { clearTimeout(this.watchdog); this.watchdog = undefined }
    }

    private closeStream(): void {
        if (this.stream) { this.stream.close(); this.stream = null }
    }
}
