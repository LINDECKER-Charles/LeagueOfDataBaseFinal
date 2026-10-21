import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';

/** What a pass had processed at an instant of the server. */
export interface PaceSample {
  /** generatedAt of the answer, in milliseconds. */
  readonly at: number;
  /** Pages written, skipped or failed. */
  readonly processed: number;
  readonly bytes: number;
}

const RUNNING = 'running';
const MINUTE_MS = 60_000;
// 360 answers 5 seconds apart: the last half hour.
const MAX_SAMPLES = 360;
// The instant pace looks two minutes back, and needs 20 seconds of samples.
const INSTANT_MS = 2 * MINUTE_MS;
const INSTANT_MIN_MS = 20_000;
// The recent pace, for the end at the current pace, and the curve look 15 minutes back.
const RECENT_MS = 15 * MINUTE_MS;
const RECENT_MIN_MS = 5 * MINUTE_MS;
// A hole wider than this breaks the curve: nothing is drawn where nothing was seen.
const GAP_MS = 2 * MINUTE_MS;
const SMOOTHING = 3;

function rate(from: PaceSample, to: PaceSample): number {
  return ((to.processed - from.processed) / (to.at - from.at)) * MINUTE_MS;
}

// A moving mean of 3 within a run: a null breaks the run and stays where it is.
function smoothed(rates: readonly (number | null)[]): (number | null)[] {
  return rates.map((value, index) => {
    if (value === null) {
      return null;
    }
    const window: number[] = [value];
    for (let back = index - 1; back > index - SMOOTHING && back >= 0; back--) {
      const earlier = rates[back];
      if (earlier === null || earlier === undefined) {
        break;
      }
      window.push(earlier);
    }
    return window.reduce((sum, item) => sum + item, 0) / window.length;
  });
}

/**
 * What the monitor saw a running pass process, answer after answer (§ 4.3), immutable: at
 * most 360 samples, one per answer of a running pass newer than the last. Another pass, or
 * a count that goes back, starts over. A missed answer invents nothing: a pace is always
 * measured over the real time between two samples, and a hole over two minutes breaks the
 * curve. Nothing is kept on the server: the curve fills from the opening of the page.
 */
export class PaceSamples {
  private constructor(
    private readonly passId: number | null,
    readonly samples: readonly PaceSample[],
  ) {}

  static empty(): PaceSamples {
    return new PaceSamples(null, []);
  }

  /** With the sample of `pass` at `at`, the server's instant of the answer. */
  add(pass: PrerenderPassView, at: number): PaceSamples {
    const sample = {
      at,
      processed: pass.pagesWritten + pass.pagesSkipped + pass.pagesFailed,
      bytes: pass.bytesWritten,
    };
    const last = this.samples.at(-1);
    if (pass.id !== this.passId || (last && sample.processed < last.processed)) {
      return new PaceSamples(pass.id, pass.state === RUNNING ? [sample] : []);
    }
    if (pass.state !== RUNNING || (last && at <= last.at)) {
      return this;
    }
    return new PaceSamples(pass.id, [...this.samples, sample].slice(-MAX_SAMPLES));
  }

  /** Pages a minute over the last 2 minutes; null under 20 seconds of samples. */
  instant(): number | null {
    return this.paceOver(INSTANT_MS, INSTANT_MIN_MS);
  }

  /** Pages a minute over the last 15 minutes; null under 5 minutes of samples. */
  recent(): number | null {
    return this.paceOver(RECENT_MS, RECENT_MIN_MS);
  }

  /** Whether too little was seen yet for a pace: « mesure en cours… ». */
  isMeasuring(): boolean {
    return this.instant() === null;
  }

  /**
   * The pace of each interval of the last 15 minutes, smoothed by a moving mean of 3; null
   * where a hole over two minutes breaks the curve.
   */
  rates(): (number | null)[] {
    const last = this.samples.at(-1);
    if (!last) {
      return [];
    }
    const kept = this.samples.filter((sample) => sample.at >= last.at - RECENT_MS);
    const rates = kept
      .slice(1)
      .map((sample, index) =>
        sample.at - kept[index].at > GAP_MS ? null : rate(kept[index], sample),
      );
    return smoothed(rates);
  }

  // From the oldest sample of the window; after a hole that left the window empty, from the
  // sample before it, so that a hole of 3 minutes gives the mean pace of those 3 minutes.
  private paceOver(window: number, minimum: number): number | null {
    const last = this.samples.at(-1);
    const end = this.samples.length - 1;
    const start = this.samples.findIndex((sample) => last && sample.at >= last.at - window);
    const first = this.samples[start === end ? start - 1 : start];
    if (!last || !first || last.at - first.at < minimum) {
      return null;
    }
    return rate(first, last);
  }
}
