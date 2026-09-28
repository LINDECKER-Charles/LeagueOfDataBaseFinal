/**
 * Whether a reveal target skips the rise: the reader asked for reduced motion, or the target
 * already sits in the first screen when the directive arms it, where animating would hide
 * content that was just painted.
 */
export function shouldRevealImmediately(
  reducedMotion: boolean,
  top: number,
  viewportHeight: number,
): boolean {
  return reducedMotion || top < viewportHeight;
}
