import { readRenderTimeouts } from './read-render-timeouts';

describe('readRenderTimeouts', () => {
  it('stops a render at 20 s and each call at 10 s by default', () => {
    expect(readRenderTimeouts({})).toEqual({ deadlineMs: 20_000, apiCallMs: 10_000 });
  });

  it('reads the deployment variables, in seconds', () => {
    const env = { LODB_RENDER_DEADLINE_SECONDS: '60', LODB_RENDER_API_TIMEOUT_SECONDS: '30' };

    expect(readRenderTimeouts(env)).toEqual({ deadlineMs: 60_000, apiCallMs: 30_000 });
  });

  it.each(['0', '2.5', 'soon'])('refuses LODB_RENDER_DEADLINE_SECONDS=%s', (value) => {
    expect(() => readRenderTimeouts({ LODB_RENDER_DEADLINE_SECONDS: value })).toThrow(
      /LODB_RENDER_DEADLINE_SECONDS/,
    );
  });

  it('refuses a call longer than the whole render', () => {
    expect(() =>
      readRenderTimeouts({
        LODB_RENDER_DEADLINE_SECONDS: '5',
        LODB_RENDER_API_TIMEOUT_SECONDS: '6',
      }),
    ).toThrow(/LODB_RENDER_API_TIMEOUT_SECONDS/);
  });
});
