import { renderLaneOf } from './render-lane-of';

describe('renderLaneOf', () => {
  it.each([
    '/fr/15.13.1/items/223193-gargoyle-stoneplate',
    '/ru/16.15.1/champions',
    '/en/7.9.1/',
    '/en/champions?version=15.14.1',
    '/en/items/3031-infinity-edge?tag=Boots&version=14.24.1',
  ])('puts %s in the pinned lane', (url) => {
    expect(renderLaneOf(url)).toBe('pinned');
  });

  it.each([
    '/fr/',
    '/en/champions',
    '/de/items/3031-infinity-edge?tag=Boots',
    '/en/about',
    '/b/abcdef',
    // Not a locale first: no version segment can follow.
    '/15.13.1/champions',
  ])('puts %s in the current lane', (url) => {
    expect(renderLaneOf(url)).toBe('current');
  });
});
