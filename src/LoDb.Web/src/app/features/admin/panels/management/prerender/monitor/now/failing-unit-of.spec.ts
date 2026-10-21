import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { failingUnitOf } from './failing-unit-of';

const BASE = prerenderProgress();

function running(written: number, failed: number, state = 'running') {
  return prerenderProgress({
    pass: { ...BASE.pass, state },
    running: [{ ...BASE.running[0], pagesWritten: written, pagesSkipped: 0, pagesFailed: failed }],
  });
}

describe('failingUnitOf', () => {
  it('says nothing while the running unit holds', () => {
    expect(failingUnitOf(BASE)).toBeNull();
    expect(failingUnitOf(running(9, 1))).toBeNull();
  });

  it('names the running unit a fifth of whose pages failed', () => {
    const failing = failingUnitOf(running(6, 8));

    expect(failing?.unit.locale).toBe('fr');
    expect(failing?.processed).toBe(14);
    expect(failing?.share).toBeCloseTo(8 / 14);
  });

  it('waits for 10 pages before it tells', () => {
    expect(failingUnitOf(running(2, 7))).toBeNull();
  });

  it('never for a paused pass', () => {
    expect(failingUnitOf(running(6, 8, 'paused'))).toBeNull();
  });
});
