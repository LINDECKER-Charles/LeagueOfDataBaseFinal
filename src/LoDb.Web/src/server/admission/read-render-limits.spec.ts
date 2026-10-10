import { readRenderLimits } from './read-render-limits';

describe('readRenderLimits', () => {
  it('has defaults without any variable', () => {
    expect(readRenderLimits({})).toEqual({
      maxInFlight: 8,
      pinnedMaxInFlight: 3,
      pinnedPerMinute: 60,
      pinnedBurst: 10,
      currentPerMinute: 300,
      currentBurst: 60,
      perClientMaxInFlight: 2,
      pinnedPerClientPerMinute: 6,
      trackedClients: 50_000,
    });
  });

  it('reads the deployment variables', () => {
    const limits = readRenderLimits({
      LODB_RENDER_MAX_IN_FLIGHT: '12',
      LODB_PINNED_RENDER_MAX_IN_FLIGHT: '2',
      LODB_PINNED_RENDERS_PER_MINUTE: '30',
      LODB_PINNED_RENDER_BURST: '',
      LODB_CURRENT_RENDERS_PER_MINUTE: '600',
      LODB_CURRENT_RENDER_BURST: '100',
      LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT: '4',
      LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE: '12',
      LODB_RENDER_TRACKED_CLIENTS: '1000',
    });

    expect(limits).toEqual({
      maxInFlight: 12,
      pinnedMaxInFlight: 2,
      pinnedPerMinute: 30,
      pinnedBurst: 10,
      currentPerMinute: 600,
      currentBurst: 100,
      perClientMaxInFlight: 4,
      pinnedPerClientPerMinute: 12,
      trackedClients: 1000,
    });
  });

  it.each([
    ['LODB_PINNED_RENDERS_PER_MINUTE', '0'],
    ['LODB_PINNED_RENDERS_PER_MINUTE', '-1'],
    ['LODB_CURRENT_RENDERS_PER_MINUTE', '1.5'],
    ['LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT', 'many'],
  ])('refuses %s=%s', (variable, value) => {
    expect(() => readRenderLimits({ [variable]: value })).toThrow(new RegExp(variable));
  });

  it('refuses a pinned lane that could take every slot', () => {
    expect(() =>
      readRenderLimits({ LODB_RENDER_MAX_IN_FLIGHT: '4', LODB_PINNED_RENDER_MAX_IN_FLIGHT: '4' }),
    ).toThrow(/LODB_PINNED_RENDER_MAX_IN_FLIGHT/);
  });

  it('lets one client take every slot, never more', () => {
    const all = { LODB_RENDER_MAX_IN_FLIGHT: '4', LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT: '4' };

    expect(readRenderLimits(all).perClientMaxInFlight).toBe(4);
    expect(() => readRenderLimits({ ...all, LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT: '5' })).toThrow(
      /LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT/,
    );
  });

  it('refuses a client budget of pinned renders above the whole lane', () => {
    expect(() =>
      readRenderLimits({
        LODB_PINNED_RENDERS_PER_MINUTE: '10',
        LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE: '11',
      }),
    ).toThrow(/LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE/);
  });
});
