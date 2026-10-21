import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { PaceSamples } from './pace-samples';

const PASS = prerenderProgress().pass;
const START = Date.parse('2026-10-21T08:00:00Z');
const SECOND = 1_000;
const MINUTE = 60 * SECOND;

// The pass having written `pages` more than in the fixture.
function at(pages: number, changes: Partial<PrerenderPassView> = {}): PrerenderPassView {
  return { ...PASS, pagesWritten: PASS.pagesWritten + pages, ...changes };
}

// Samples every `step` ms at `perMinute` pages a minute, `count` of them.
function steady(count: number, step: number, perMinute: number): PaceSamples {
  let samples = PaceSamples.empty();
  for (let index = 0; index < count; index++) {
    const elapsed = index * step;
    samples = samples.add(at((elapsed / MINUTE) * perMinute), START + elapsed);
  }
  return samples;
}

describe('PaceSamples', () => {
  it('adds a sample of a running pass, never twice at the same instant', () => {
    const samples = PaceSamples.empty().add(at(0), START).add(at(10), START);

    expect(samples.samples).toEqual([
      { at: START, processed: 1_532_410, bytes: PASS.bytesWritten },
    ]);
  });

  it('adds nothing for a paused pass', () => {
    const samples = PaceSamples.empty()
      .add(at(0), START)
      .add(at(0, { state: 'paused' }), START + 5 * SECOND);

    expect(samples.samples).toHaveLength(1);
  });

  it('starts over for another pass, or a count that goes back', () => {
    const base = steady(5, 5 * SECOND, 240);

    expect(base.add(at(0, { id: 13 }), START + MINUTE).samples).toHaveLength(1);
    expect(base.add(at(-100), START + MINUTE).samples).toHaveLength(1);
  });

  it('measures the instant pace over the last two minutes', () => {
    expect(steady(60, 5 * SECOND, 240).instant()).toBeCloseTo(240);
  });

  it('knows no instant pace under 20 seconds of samples', () => {
    const samples = steady(4, 5 * SECOND, 240);

    expect(samples.instant()).toBeNull();
    expect(samples.isMeasuring()).toBe(true);
  });

  it('measures the recent pace over 15 minutes, once 5 minutes are seen', () => {
    expect(steady(12, 20 * SECOND, 240).recent()).toBeNull();
    expect(steady(60, 20 * SECOND, 120).recent()).toBeCloseTo(120);
  });

  it('measures over a hole without inventing samples, and breaks the curve there', () => {
    // 45 seconds at 240 pages a minute (180 pages), then nothing seen for 3 minutes.
    const before = steady(10, 5 * SECOND, 240);
    const after = before.add(at(180 + 3 * 240), START + 45 * SECOND + 3 * MINUTE);

    expect(after.instant()).toBeCloseTo(240);
    expect(after.rates().at(-1)).toBeNull();
    expect(after.rates().filter((rate) => rate !== null)).toHaveLength(9);
  });

  it('smooths the curve by a moving mean of three', () => {
    const samples = PaceSamples.empty()
      .add(at(0), START)
      .add(at(10), START + MINUTE)
      .add(at(30), START + 2 * MINUTE)
      .add(at(60), START + 3 * MINUTE);

    expect(samples.rates()).toEqual([10, 15, 20]);
  });

  it('keeps 360 samples at most', () => {
    expect(steady(400, 5 * SECOND, 240).samples).toHaveLength(360);
  });
});
