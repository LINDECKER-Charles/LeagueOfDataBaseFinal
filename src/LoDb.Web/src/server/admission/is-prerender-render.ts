import type { Request } from 'express';

// Sent by the API's pre-render pass, which calls the renderer over the stack network; nginx
// always sends it empty on public traffic (snippets/proxy-headers.conf), so that no client
// passes for the pass.
const PRERENDER_HEADER = 'x-lodb-prerender';
const PRERENDER_MARK = '1';

/**
 * Whether a request comes from the pre-render pass, which the admission runs in a lane of its
 * own (`RenderAdmission`). Only the stack reaches the site's port (`networkGate`), and nginx
 * writes the header on every request it proxies.
 */
export function isPrerenderRender(request: Pick<Request, 'get'>): boolean {
  return request.get(PRERENDER_HEADER) === PRERENDER_MARK;
}
