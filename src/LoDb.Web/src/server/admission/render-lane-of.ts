import { parsePublicUrl } from '../../app/core/routing/url/parse-public-url';
import { isVersionShaped } from '../routing/is-version-shaped';
import type { RenderLane } from './render-lane';

const VERSION_PARAMETER = 'version';

/**
 * Lane of a render, from its root-relative URL (ADR 0005 grammar), read decoded as the
 * router reads it: `/fr/%31%35.13.1/champions` is a pinned page like `/fr/15.13.1/champions`.
 */
export function renderLaneOf(url: string): RenderLane {
  const { version, query } = parsePublicUrl(url, isVersionShaped);
  return version !== null || query.get(VERSION_PARAMETER) !== null ? 'pinned' : 'current';
}
