import { CACHE_CONTROL } from '../../app/core/routing/response/cache-control';
import { cacheClassMark } from './cache-class-mark';

describe('cacheClassMark', () => {
  it('marks a render of the pass whose page is of the archived class', () => {
    expect(cacheClassMark('prerender', CACHE_CONTROL.archived)).toEqual({
      'X-LoDb-Cache-Class': 'archived',
    });
  });

  it.each([
    CACHE_CONTROL.latest,
    CACHE_CONTROL.revisable,
    CACHE_CONTROL.transient,
    CACHE_CONTROL.private,
    null,
  ])('leaves unmarked a render of the pass under %s', (cacheControl) => {
    expect(cacheClassMark('prerender', cacheControl)).toEqual({});
  });

  it.each(['pinned', 'current'] as const)('never marks a render of the %s lane', (lane) => {
    expect(cacheClassMark(lane, CACHE_CONTROL.archived)).toEqual({});
  });
});
