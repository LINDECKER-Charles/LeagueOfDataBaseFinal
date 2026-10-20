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
      currentPerClientPerMinute: 60,
      currentPerClientBurst: 20,
      trackedClients: 50_000,
      prerenderPerMinute: 120,
      prerenderBurst: 10,
      prerenderMaxInFlight: 2,
      prerenderVisitorReserve: 4,
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
      LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE: '120',
      LODB_CURRENT_RENDER_PER_CLIENT_BURST: '30',
      LODB_RENDER_TRACKED_CLIENTS: '1000',
      LODB_PRERENDER_RENDERS_PER_MINUTE: '30',
      LODB_PRERENDER_RENDER_BURST: '5',
      LODB_PRERENDER_MAX_IN_FLIGHT: '3',
      LODB_PRERENDER_VISITOR_RESERVE: '6',
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
      currentPerClientPerMinute: 120,
      currentPerClientBurst: 30,
      trackedClients: 1000,
      prerenderPerMinute: 30,
      prerenderBurst: 5,
      prerenderMaxInFlight: 3,
      prerenderVisitorReserve: 6,
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

  it.each(['LODB_PRERENDER_MAX_IN_FLIGHT', 'LODB_PRERENDER_VISITOR_RESERVE'])(
    'refuses %s set to every slot',
    (variable) => {
      expect(() => readRenderLimits({ LODB_RENDER_MAX_IN_FLIGHT: '4', [variable]: '4' })).toThrow(
        new RegExp(variable),
      );
    },
  );

  // The staging runs three slots and leaves the pre-render bounds to their defaults.
  it('fits the pre-render bounds left to their default below fewer slots', () => {
    const limits = readRenderLimits({
      LODB_RENDER_MAX_IN_FLIGHT: '3',
      LODB_PINNED_RENDER_MAX_IN_FLIGHT: '1',
    });

    expect(limits.prerenderMaxInFlight).toBe(2);
    expect(limits.prerenderVisitorReserve).toBe(2);
  });

  it('lets one client take every slot, never more', () => {
    const all = { LODB_RENDER_MAX_IN_FLIGHT: '4', LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT: '4' };

    expect(readRenderLimits(all).perClientMaxInFlight).toBe(4);
    expect(() => readRenderLimits({ ...all, LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT: '5' })).toThrow(
      /LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT/,
    );
  });

  // The lane's budget lowered in an incident, the client's left alone: the server starts.
  it.each([
    ['LODB_PINNED_RENDERS_PER_MINUTE', 'pinnedPerClientPerMinute'],
    ['LODB_CURRENT_RENDERS_PER_MINUTE', 'currentPerClientPerMinute'],
    ['LODB_CURRENT_RENDER_BURST', 'currentPerClientBurst'],
  ] as const)('caps a client share left to its default by %s set below it', (whole, part) => {
    expect(readRenderLimits({ [whole]: '4' })[part]).toBe(4);
  });

  it.each([
    ['LODB_PINNED_RENDERS_PER_MINUTE', 'LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE'],
    ['LODB_CURRENT_RENDERS_PER_MINUTE', 'LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE'],
    ['LODB_CURRENT_RENDER_BURST', 'LODB_CURRENT_RENDER_PER_CLIENT_BURST'],
  ])('lets the whole %s be reached by its client share %s, never exceeded', (whole, part) => {
    expect(() => readRenderLimits({ [whole]: '10', [part]: '10' })).not.toThrow();
    expect(() => readRenderLimits({ [whole]: '10', [part]: '11' })).toThrow(new RegExp(part));
  });
});
