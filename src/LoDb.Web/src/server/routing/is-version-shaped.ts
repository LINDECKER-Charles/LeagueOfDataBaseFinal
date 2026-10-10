// No page segment starts with a digit, so after the locale such a segment is a version.
const VERSION_SHAPE = /^\d/;

/**
 * Whether a segment right after the locale reads as a version. The server decides before any
 * API call, hence a shape and not the pattern of `/api/meta`: a segment it misjudges only
 * moves a render to the other lane, or a list's query to the rules of a detail.
 */
export function isVersionShaped(segment: string): boolean {
  return VERSION_SHAPE.test(segment);
}
