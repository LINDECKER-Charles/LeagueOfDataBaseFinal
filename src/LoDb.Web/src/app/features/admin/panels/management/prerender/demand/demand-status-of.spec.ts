import { prerenderDemand } from '../../../../testing/fixtures/prerender-demand';
import { demandStatusOf } from './demand-status-of';

describe('demandStatusOf', () => {
  it('reads an active queue that reads the SSR as active, without a reason', () => {
    expect(demandStatusOf(prerenderDemand())).toEqual({ state: 'active', tone: 'good', why: null });
  });

  it.each([
    ['switched off', { isEnabled: false, isActive: false, isCapturing: false }, 'off'],
    ['without a store', { isActive: false, isCapturing: false }, 'dormant'],
    ['without reading the SSR', { isCapturing: false }, 'blind'],
  ] as const)('says why a queue %s does not run fully', (_what, overrides, state) => {
    const status = demandStatusOf(prerenderDemand(overrides));

    expect(status.state).toBe(state);
    expect(status.why).toBe(state);
  });

  it('says the switch first, before a store that is missing too', () => {
    const status = demandStatusOf(prerenderDemand({ isEnabled: false, isActive: false }));

    expect(status.state).toBe('off');
  });
});
