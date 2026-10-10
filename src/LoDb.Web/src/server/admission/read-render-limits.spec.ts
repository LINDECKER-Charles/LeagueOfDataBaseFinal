import { readRenderLimits } from './read-render-limits';

describe('readRenderLimits', () => {
  it('has defaults without any variable', () => {
    expect(readRenderLimits({})).toEqual({
      maxInFlight: 8,
      pinnedMaxInFlight: 3,
      pinnedPerMinute: 60,
      pinnedBurst: 10,
    });
  });

  it('reads the deployment variables', () => {
    const limits = readRenderLimits({
      LODB_RENDER_MAX_IN_FLIGHT: '12',
      LODB_PINNED_RENDER_MAX_IN_FLIGHT: '2',
      LODB_PINNED_RENDERS_PER_MINUTE: '30',
      LODB_PINNED_RENDER_BURST: '',
    });

    expect(limits).toEqual({
      maxInFlight: 12,
      pinnedMaxInFlight: 2,
      pinnedPerMinute: 30,
      pinnedBurst: 10,
    });
  });

  it.each(['0', '-1', '1.5', 'many'])('refuses LODB_PINNED_RENDERS_PER_MINUTE=%s', (value) => {
    expect(() => readRenderLimits({ LODB_PINNED_RENDERS_PER_MINUTE: value })).toThrow(
      /LODB_PINNED_RENDERS_PER_MINUTE/,
    );
  });

  it('refuses a pinned lane that could take every slot', () => {
    expect(() =>
      readRenderLimits({ LODB_RENDER_MAX_IN_FLIGHT: '4', LODB_PINNED_RENDER_MAX_IN_FLIGHT: '4' }),
    ).toThrow(/LODB_PINNED_RENDER_MAX_IN_FLIGHT/);
  });
});
