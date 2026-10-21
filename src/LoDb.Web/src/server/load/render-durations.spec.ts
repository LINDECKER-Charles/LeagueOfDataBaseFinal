import { RenderDurations } from './render-durations';

describe('RenderDurations', () => {
  it('sums the renders and their durations per lane, from zero', () => {
    const durations = new RenderDurations();

    durations.add('prerender', 0.5);
    durations.add('prerender', 0.25);
    durations.add('current', 0.1);

    expect(durations.of('prerender')).toEqual({ renders: 2, renderSeconds: 0.75 });
    expect(durations.of('current')).toEqual({ renders: 1, renderSeconds: 0.1 });
    expect(durations.of('pinned')).toEqual({ renders: 0, renderSeconds: 0 });
  });
});
