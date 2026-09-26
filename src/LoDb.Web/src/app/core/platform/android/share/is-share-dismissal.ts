// The only message @capacitor/share rejects with when the user closes the sheet without
// picking a target; it has no error code to test instead.
const SHARE_CANCELED = 'Share canceled';

/** Whether a rejected native share is the user closing the sheet, not a failure. */
export function isShareDismissal(error: unknown): boolean {
  return error instanceof Error && error.message === SHARE_CANCELED;
}
