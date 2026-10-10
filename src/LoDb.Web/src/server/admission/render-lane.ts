/**
 * Lanes of the render admission: `pinned` for a page of a version named in its URL (an older
 * patch, the long tail that crawlers sweep), `current` for every other page.
 */
export type RenderLane = 'pinned' | 'current';
