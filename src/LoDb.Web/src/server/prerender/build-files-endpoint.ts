import type { RequestHandler } from 'express';
import { isPrerenderRender } from '../admission/is-prerender-render';
import type { BuildListing } from './build-listing';

const NOT_FOUND = 404;

/**
 * Answers the pre-render pass the hashed files of this build (`readBuildListing`). The pass
 * copies the whole set before any page of the build: a page names its entry bundles only, the
 * lazy chunks (routes, `@defer`) are named inside the scripts, and the shared folder
 * (`front-builds`) prunes a build's files some weeks after it.
 * Without the pass's header, a bare 404: nginx clears it on public traffic.
 */
export function buildFilesEndpoint(listing: BuildListing): RequestHandler {
  return (request, response) => {
    if (!isPrerenderRender(request)) {
      response.status(NOT_FOUND).type('text/plain').send('Not Found');
      return;
    }
    response.set('Cache-Control', 'no-store').json(listing);
  };
}
