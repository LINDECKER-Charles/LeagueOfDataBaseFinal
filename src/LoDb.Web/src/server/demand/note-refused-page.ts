import type { Response } from 'express';
import type { RenderRequest } from '../admission/render-request';
import { SHED_MARKER } from '../admission/shed-marker';
import { refusedPagePathOf } from './refused-page-path';
import type { RefusedPages } from './refused-pages';

/**
 * Counts the page of an older patch that a visitor's render was refused (`RenderAdmission`'s
 * 503), for the API's automatic queue. The pass's own refusals are never counted: they are
 * its pace, not a demand.
 */
export function noteRefusedPage(
  pages: RefusedPages,
  request: RenderRequest,
  response: Pick<Response, 'locals'>,
): void {
  if (request.isPrerender || response.locals[SHED_MARKER] !== true) {
    return;
  }
  const path = refusedPagePathOf(request.url);
  if (path !== null) {
    pages.record(path);
  }
}
