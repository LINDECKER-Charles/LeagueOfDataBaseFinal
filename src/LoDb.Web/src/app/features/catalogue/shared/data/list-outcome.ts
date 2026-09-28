/**
 * What a list call gave: the list, or nothing yet because the version is still being
 * ingested (`pending`, the API's 503), or a failure. `retryAfterMs` is the delay the API
 * asked for before one more try: on a 503, or on a list whose images are still placeholders.
 */
export type ListOutcome<L> =
  | { readonly kind: 'list'; readonly list: L; readonly retryAfterMs: number | null }
  | { readonly kind: 'pending'; readonly retryAfterMs: number | null }
  | { readonly kind: 'failed' };
