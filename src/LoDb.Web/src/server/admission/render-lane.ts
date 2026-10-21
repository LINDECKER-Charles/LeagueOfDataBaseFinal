/**
 * Lanes of the render admission: `pinned` for a page of a version named in its URL (an older
 * patch, the long tail that crawlers sweep), `current` for every other page, and `prerender`
 * for the renders the API's pre-render pass asks for (`isPrerenderRender`), never a visitor's,
 * whatever their URL.
 */
export type RenderLane = 'pinned' | 'current' | 'prerender';
