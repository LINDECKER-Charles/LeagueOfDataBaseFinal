import { CACHE_CONTROL } from '../../app/core/routing/response/cache-control';
import { cacheClassMark } from './cache-class-mark';

describe('cacheClassMark', () => {
  it.each(['archived', 'revisable'] as const)(
    'marks a render of the pass whose page is of the %s class',
    (cacheClass) => {
      expect(cacheClassMark('prerender', CACHE_CONTROL[cacheClass])).toEqual({
        'X-LoDb-Cache-Class': cacheClass,
      });
    },
  );

  it.each([CACHE_CONTROL.latest, CACHE_CONTROL.transient, CACHE_CONTROL.private, null])(
    'leaves unmarked a render of the pass under %s',
    (cacheControl) => {
      expect(cacheClassMark('prerender', cacheControl)).toEqual({});
    },
  );

  it.each(['pinned', 'current'] as const)('never marks a render of the %s lane', (lane) => {
    expect(cacheClassMark(lane, CACHE_CONTROL.archived)).toEqual({});
    expect(cacheClassMark(lane, CACHE_CONTROL.revisable)).toEqual({});
  });
});
