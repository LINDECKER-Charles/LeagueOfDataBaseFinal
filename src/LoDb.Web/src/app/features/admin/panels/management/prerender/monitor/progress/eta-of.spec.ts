import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { type EtaInput, etaOf } from './eta-of';

const PASS = prerenderProgress().pass;
const NOW = Date.parse('2026-10-21T08:42:05+00:00');
const AVERAGE = Date.parse('2026-10-23T14:00:00+00:00');
const HOUR = 3_600_000;
// The pace at which the 613,590 pages left end exactly at the server's estimate.
const SAME_PACE = (PASS.pagesLeft ?? 0) / ((AVERAGE - NOW) / 60_000);

function input(recentPace: number | null, changes: Partial<EtaInput> = {}): EtaInput {
  return { pass: PASS, recentPace, serverNow: NOW, ...changes };
}

describe('etaOf', () => {
  it('gives the mean end alone while the recent pace is unknown', () => {
    expect(etaOf(input(null), false)).toEqual({
      state: 'known',
      average: AVERAGE,
      current: null,
      isCurrentShown: false,
    });
  });

  it('shows the end at the current pace once they differ by more than 15 %', () => {
    const eta = etaOf(input(SAME_PACE * 1.25), false);

    expect(eta.isCurrentShown).toBe(true);
    expect(eta.current).toBeLessThan(AVERAGE);
  });

  it('keeps its previous state between 5 and 15 %', () => {
    expect(etaOf(input(SAME_PACE * 1.1), true).isCurrentShown).toBe(true);
    expect(etaOf(input(SAME_PACE * 1.1), false).isCurrentShown).toBe(false);
  });

  it('hides it again under 5 %', () => {
    expect(etaOf(input(SAME_PACE * 1.02), true).isCurrentShown).toBe(false);
  });

  it('knows no end without any pace', () => {
    const pass = { ...PASS, estimatedEndAt: null };

    expect(etaOf(input(null, { pass }), true).state).toBe('unknown');
    expect(etaOf(input(SAME_PACE, { pass }), false).current).toBeCloseTo(AVERAGE, -4);
  });

  it('holds the end while paused', () => {
    const pass = { ...PASS, state: 'paused' };

    expect(etaOf(input(SAME_PACE, { pass }), true)).toEqual({
      state: 'paused',
      average: null,
      current: null,
      isCurrentShown: false,
    });
  });

  it('ends now at the current pace once nothing is left', () => {
    const pass = { ...PASS, pagesLeft: 0, estimatedEndAt: new Date(NOW + HOUR).toISOString() };

    expect(etaOf(input(SAME_PACE, { pass }), false).current).toBe(NOW);
  });
});
