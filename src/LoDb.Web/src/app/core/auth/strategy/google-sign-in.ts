/** What a sign-in through Google needs to come back to the right page. */
export interface GoogleSignIn {
  /** The page to come back to, as the login page received it: untrusted. */
  readonly returnUrl: string | null | undefined;
  /** Where to come back when `returnUrl` is missing or leaves the application. */
  readonly fallbackUrl: string;
  /** Locale of the pages to come back to, such as `fr`. */
  readonly locale: string;
  /** Keeps the session for 30 days rather than until the browser closes. */
  readonly rememberMe: boolean;
}
