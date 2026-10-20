import type { RenderLane } from './render-lane';

/**
 * The lanes of the renders visitors ask for, each with a share per client: the prerender lane
 * has a single client, the API's pass, bounded by limits of its own instead.
 */
export type VisitorLane = Exclude<RenderLane, 'prerender'>;
