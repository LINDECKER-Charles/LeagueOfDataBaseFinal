import { parsePublicUrl } from '../../app/core/routing/url/parse-public-url';
import type { RenderLane } from './render-lane';

// No page segment starts with a digit, so after the locale such a segment is a version.
// Admission decides before any API call, hence a shape and not the pattern of `/api/meta`:
// a segment it misjudges only moves a render to the other lane.
const VERSION_SHAPE = /^\d/;
const VERSION_PARAMETER = 'version';

/** Lane of a render, from its root-relative URL (ADR 0005 grammar). */
export function renderLaneOf(url: string): RenderLane {
  const { version, query } = parsePublicUrl(url, (segment) => VERSION_SHAPE.test(segment));
  return version !== null || query.get(VERSION_PARAMETER) !== null ? 'pinned' : 'current';
}
