import { buildMark } from './build-mark';

const BUILD = 'b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c';

describe('buildMark', () => {
  it('names the build on a render of the pass', () => {
    expect(buildMark('prerender', BUILD)).toEqual({ 'X-LoDb-Build': BUILD });
  });

  it('leaves a render of the pass unmarked when the build has no listing', () => {
    expect(buildMark('prerender', null)).toEqual({});
  });

  it.each(['pinned', 'current'] as const)('never marks a render of the %s lane', (lane) => {
    expect(buildMark(lane, BUILD)).toEqual({});
  });
});
