import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { alarmOf } from './alarm-of';

const BASE = prerenderProgress();
const FAILING = { unit: BASE.running[0], processed: 14, share: 8 / 14 };
const STALLED = { kind: 'stalled', cause: 'unknown', idleSeconds: 240 } as const;

describe('alarmOf', () => {
  it('says nothing while all is well', () => {
    expect(alarmOf(BASE, null, null)).toBeNull();
    expect(alarmOf(undefined, null, null)).toBeNull();
  });

  it('names the first thing going wrong, a failing unit first', () => {
    const off = prerenderProgress({ pulse: { ...BASE.pulse, isWorkerEnabled: false } });

    expect(alarmOf(off, STALLED, FAILING)).toEqual({
      kind: 'failing',
      version: '16.18.1',
      locale: 'fr',
    });
    expect(alarmOf(off, STALLED, null)).toEqual({ kind: 'stalled', cause: 'unknown' });
    expect(alarmOf(off, { kind: 'starting' }, null)).toEqual({ kind: 'worker_off' });
  });

  it('says the SSR is out of reach only while the pass runs', () => {
    const pulse = { ...BASE.pulse, ssrUnreachableSince: '2026-10-21T08:40:00+00:00' };
    const paused = prerenderProgress({ pulse, pass: { ...BASE.pass, state: 'paused' } });

    expect(alarmOf(prerenderProgress({ pulse }), null, null)).toEqual({ kind: 'ssr' });
    expect(alarmOf(paused, null, null)).toBeNull();
  });

  it('stays the same while the same unit fails, whatever its counts', () => {
    const later = { ...FAILING, processed: 40, share: 0.9 };

    expect(alarmOf(BASE, null, later)).toEqual(alarmOf(BASE, null, FAILING));
  });
});
