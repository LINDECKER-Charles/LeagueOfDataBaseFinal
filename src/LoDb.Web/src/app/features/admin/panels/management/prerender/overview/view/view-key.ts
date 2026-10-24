import { PRERENDER_VIEWS, type PrerenderView } from './prerender-view';

const PREFIX = 'prerender-';

/**
 * The key of a view's tab and panel, `prerender-coverage`: its ids, `{key}-tab` and
 * `{key}-panel`, stay unique in the page. A key that names no view reads null.
 */
export const viewKey = {
  of: (view: PrerenderView): string => `${PREFIX}${view}`,
  view: (key: string): PrerenderView | null => {
    const view = key.startsWith(PREFIX) ? key.slice(PREFIX.length) : null;
    return (PRERENDER_VIEWS as readonly (string | null)[]).includes(view)
      ? (view as PrerenderView)
      : null;
  },
};
