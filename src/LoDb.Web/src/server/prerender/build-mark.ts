import type { RenderLane } from '../admission/render-lane';

const BUILD_HEADER = 'X-LoDb-Build';

/**
 * The header a render of the pre-render pass gets to name the build that rendered it,
 * `X-LoDb-Build: <digest>`, the `build` of `/_lodb/build-files`. The renderer may be
 * redeployed in the middle of the pass: the API copies the build a page names before the
 * page, and stores the digest with it, rather than trust a list it fetched earlier.
 * None for any other lane, nor when the build has no listing (under `ng serve`).
 */
export function buildMark(
  lane: RenderLane,
  build: string | null,
): Readonly<Record<string, string>> {
  return lane === 'prerender' && build !== null ? { [BUILD_HEADER]: build } : {};
}
