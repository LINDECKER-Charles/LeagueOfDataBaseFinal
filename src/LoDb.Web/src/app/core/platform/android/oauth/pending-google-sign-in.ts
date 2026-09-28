/**
 * A Google sign-in opened in the system browser and not yet back. Kept in the secure storage:
 * Android may end the app while the browser is in front, and the App Link then starts it cold.
 */
export interface PendingGoogleSignIn {
  readonly clientId: string;
  readonly redirectUri: string;
  /** Echoed by Google: a return that does not carry it was not asked by this app. */
  readonly state: string;
  readonly verifier: string;
  /** Root-relative, already checked by the strategy's `landingUrl`. */
  readonly landingUrl: string;
  readonly locale: string;
  readonly isRemembered: boolean;
  /** Epoch milliseconds, for the lifetime of the flow. */
  readonly startedAt: number;
}
