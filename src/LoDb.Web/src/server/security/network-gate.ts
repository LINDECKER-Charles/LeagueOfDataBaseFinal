import type { RequestHandler } from 'express';
import type { StackPeers } from './stack-peers';

const NOT_FOUND = 404;

/**
 * Answers a bare 404, and nothing else, to a connection on the site's port that comes neither
 * from the stack's networks nor from the loopback (`StackPeers`).
 *
 * On a served host the renderer also sits on a network shared with other projects
 * (`observability`, for the scrape of its metrics port). Without the gate, their containers
 * would reach the site's port past nginx, its limits and its cache, and write the
 * `X-Forwarded-For` that `trust proxy` takes for the visitor's address. The gate judges the
 * socket's own address, never a header. Mounted before the request log: a refusal writes no
 * line, `onRefusal` counts it.
 */
export function networkGate(
  peers: Pick<StackPeers, 'has'>,
  onRefusal: () => void = () => undefined,
): RequestHandler {
  return (request, response, next) => {
    if (peers.has(request.socket.remoteAddress)) {
      next();
      return;
    }
    onRefusal();
    response.status(NOT_FOUND).end();
  };
}
