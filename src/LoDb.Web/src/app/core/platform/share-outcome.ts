/**
 * What a share request ended as, so the caller can word its feedback: a native share sheet,
 * a link copied to the clipboard instead, or a sheet the user closed.
 */
export type ShareOutcome = 'shared' | 'copied' | 'dismissed';
