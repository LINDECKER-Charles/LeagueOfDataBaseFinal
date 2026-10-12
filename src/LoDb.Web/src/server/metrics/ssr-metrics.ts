import type { ServerResponse } from 'node:http';
import { collectDefaultMetrics, Counter, Gauge, Histogram, Registry } from 'prom-client';
import type { RenderAdmission } from '../admission/render-admission';
import type { RenderCounts } from '../admission/render-counts';
import type { RenderLane } from '../admission/render-lane';
import type { NormalizedRedirectReason } from './normalized-redirect-reason';
import { statusClassOf } from './status-class-of';

const LANES: readonly RenderLane[] = ['pinned', 'current'];
const REDIRECT_REASONS: readonly NormalizedRedirectReason[] = ['query', 'slash', 'canonical'];
// From a render on a catalog the API holds (about 0.1 s) to the render deadline (20 s), with
// the bounds the alerts read: 1.5 s (a miss of the latest patch) and 3 s (a slow site).
const RENDER_SECONDS_BUCKETS = [0.1, 0.25, 0.5, 1, 1.5, 3, 5, 10, 20];
const PERMANENT_REDIRECTS: ReadonlySet<number> = new Set([301, 308]);

type InFlight = Pick<RenderAdmission, 'inFlightOf'>;

// Read from the admission's own counts at each scrape, so that nothing is counted twice. Both
// lanes have their admitted series from the start: a rate reads 0 rather than nothing.
function rendersTotal(counts: RenderCounts): Counter<'lane' | 'outcome' | 'reason'> {
  return new Counter({
    name: 'lodb_ssr_renders_total',
    help: 'Renders the admission decided on, per lane, outcome and refusal reason.',
    labelNames: ['lane', 'outcome', 'reason'],
    registers: [],
    collect() {
      this.reset();
      LANES.forEach((lane) => this.inc({ lane, outcome: 'admitted' }, 0));
      for (const { lane, refusal, count } of counts.entries()) {
        const labels =
          refusal === null
            ? { lane, outcome: 'admitted' }
            : { lane, outcome: 'refused', reason: refusal };
        this.inc(labels, count);
      }
    },
  });
}

function rendersInFlight(admission: InFlight): Gauge<'lane'> {
  return new Gauge({
    name: 'lodb_ssr_renders_in_flight',
    help: 'Renders running now, per lane.',
    labelNames: ['lane'],
    registers: [],
    collect() {
      LANES.forEach((lane) => this.set({ lane }, admission.inFlightOf(lane)));
    },
  });
}

/**
 * The renderer's metrics (docs/guides/metriques.md), in a registry of their own that the
 * metrics port serves (`serveMetrics`): the admission's decisions and renders in flight, read
 * from the admission at each scrape; the duration of the renders; the normalizing redirects;
 * the network gate's refusals; the inline scripts the pages' policy left out; and Node's own
 * (event loop lag, heap, garbage collection). No label carries a path, a version, a language
 * or an address.
 */
export class SsrMetrics {
  readonly registry = new Registry();

  private readonly renderDuration = new Histogram({
    name: 'lodb_ssr_render_duration_seconds',
    help: 'Time from the admission of a render to the end of its response.',
    labelNames: ['lane', 'status_class'] as const,
    buckets: RENDER_SECONDS_BUCKETS,
    registers: [],
  });

  private readonly redirects = new Counter({
    name: 'lodb_ssr_normalized_redirects_total',
    help: 'Redirects to the URL a page is known by, per reason.',
    labelNames: ['reason'] as const,
    registers: [],
  });

  // The network gate's only signal: it writes no log line per refusal.
  private readonly gateRefusals = new Counter({
    name: 'lodb_ssr_network_gate_refusals_total',
    help: "Requests to the site's port from outside the stack's networks.",
    registers: [],
  });

  private readonly unexpectedScripts = new Counter({
    name: 'lodb_ssr_csp_unexpected_scripts_total',
    help: 'Inline scripts of rendered pages that the build never wrote, left out of their policy.',
    registers: [],
  });

  constructor(counts: RenderCounts, admission: InFlight) {
    collectDefaultMetrics({ register: this.registry });
    REDIRECT_REASONS.forEach((reason) => this.redirects.inc({ reason }, 0));
    this.registry.registerMetric(rendersTotal(counts));
    this.registry.registerMetric(rendersInFlight(admission));
    this.registry.registerMetric(this.renderDuration);
    this.registry.registerMetric(this.redirects);
    this.registry.registerMetric(this.gateRefusals);
    this.registry.registerMetric(this.unexpectedScripts);
  }

  /** Times a render of `lane` until its response ends, under the status class it got. */
  timeRender(lane: RenderLane, response: ServerResponse): void {
    const stop = this.renderDuration.startTimer({ lane });
    response.once('close', () => stop({ status_class: statusClassOf(response) }));
  }

  /** Counts a connection the network gate turned away (`networkGate`). */
  countGateRefusal(): void {
    this.gateRefusals.inc();
  }

  /** Counts the inline scripts a rendered page held that its policy left out. */
  countUnexpectedScripts(count: number): void {
    this.unexpectedScripts.inc(count);
  }

  countRedirect(reason: NormalizedRedirectReason): void {
    this.redirects.inc({ reason });
  }

  /** Counts a render answered with a permanent redirect as a `canonical` one. */
  countRendered(status: number): void {
    if (PERMANENT_REDIRECTS.has(status)) {
      this.countRedirect('canonical');
    }
  }
}
