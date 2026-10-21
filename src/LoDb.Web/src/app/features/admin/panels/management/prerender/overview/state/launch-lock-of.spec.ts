import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { launchLockOf } from './launch-lock-of';

describe('launchLockOf', () => {
  it('waits for the first report', () => {
    expect(launchLockOf(undefined)).toEqual({ kind: 'loading' });
  });

  it('stays shut while the host is not configured, even without a pass', () => {
    expect(launchLockOf(prerenderReport({ isConfigured: false, current: null }))).toEqual({
      kind: 'dormant',
    });
  });

  it('names the pass active: one at a time', () => {
    expect(launchLockOf(prerenderReport())).toEqual({ kind: 'active', id: 3 });
  });

  it('opens once nothing holds it', () => {
    expect(launchLockOf(prerenderReport({ current: null }))).toBeNull();
  });
});
